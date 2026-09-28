import { describe, expect, it } from 'vitest';

import { EMPTY_ROW_ACCESS_CONFIG } from 'src/constants/empty-row-access-config.constant';
import { type OwnerCandidateObject } from 'src/types/owner-candidate-object.type';
import { parseRowAccessConfig } from 'src/utils/parse-row-access-config.util';
import {
  listRowAccessRuleWarnings,
  normalizeRowAccessConfig,
  removeRule,
  setRuleEnabled,
  setRuleOwnerField,
  setSeeAllRole,
} from 'src/utils/update-row-access-config.util';

const company: OwnerCandidateObject = {
  objectMetadataId: 'company',
  label: 'Company',
  ownerFields: [
    { fieldMetadataId: 'account-owner', label: 'Account Owner' },
    { fieldMetadataId: 'second-owner', label: 'Second owner' },
  ],
};

describe('parseRowAccessConfig', () => {
  it('reads the design example', () => {
    const raw =
      '{"version":1,"rules":[{"objectMetadataId":"company","ownerFieldMetadataId":"account-owner","isEnabled":true}],"seeAllRoleIds":["manager"]}';

    expect(parseRowAccessConfig(raw)).toEqual({
      config: {
        version: 1,
        rules: [
          {
            objectMetadataId: 'company',
            ownerFieldMetadataId: 'account-owner',
            isEnabled: true,
          },
        ],
        seeAllRoleIds: ['manager'],
      },
      problem: undefined,
    });
  });

  it.each([
    ['empty', ''],
    ['not JSON', '{'],
    ['a wrong version', '{"version":2,"rules":[],"seeAllRoleIds":[]}'],
    ['a broken rule', '{"version":1,"rules":[{"objectMetadataId":1}],"seeAllRoleIds":[]}'],
  ])('opens %s as an empty config with a problem', (_label, raw) => {
    const parsed = parseRowAccessConfig(raw);

    expect(parsed.config).toEqual(EMPTY_ROW_ACCESS_CONFIG);
    expect(parsed.problem).toBeDefined();
  });
});

describe('config edits', () => {
  it('turning an object on adds a rule on its first owner field; off keeps the field', () => {
    const on = setRuleEnabled(EMPTY_ROW_ACCESS_CONFIG, company, true);

    expect(on.rules).toEqual([
      {
        objectMetadataId: 'company',
        ownerFieldMetadataId: 'account-owner',
        isEnabled: true,
      },
    ]);

    const moved = setRuleOwnerField(on, 'company', 'second-owner');
    const off = setRuleEnabled(moved, company, false);

    expect(off.rules).toEqual([
      {
        objectMetadataId: 'company',
        ownerFieldMetadataId: 'second-owner',
        isEnabled: false,
      },
    ]);
  });

  it('adds and removes a see-all role once', () => {
    const added = setSeeAllRole(
      setSeeAllRole(EMPTY_ROW_ACCESS_CONFIG, 'manager', true),
      'manager',
      true,
    );

    expect(added.seeAllRoleIds).toEqual(['manager']);
    expect(setSeeAllRole(added, 'manager', false).seeAllRoleIds).toEqual([]);
  });

  it('drops roles that no longer exist and duplicate role ids on save', () => {
    expect(
      normalizeRowAccessConfig({
        config: {
          version: 1,
          rules: [],
          seeAllRoleIds: ['manager', 'deleted-role', 'manager'],
        },
        roleIds: ['manager', 'member'],
      }).seeAllRoleIds,
    ).toEqual(['manager']);
  });
});

// D35: a rule whose owner field became invalid must not vanish on Save. A
// dropped enabled rule opens every row of its object; a kept one makes the
// server fail closed until the admin fixes it.
describe('rules that became invalid (D35)', () => {
  const brokenConfig = {
    version: 1 as const,
    rules: [
      {
        objectMetadataId: 'company',
        ownerFieldMetadataId: 'account-owner',
        isEnabled: true,
      },
      {
        objectMetadataId: 'company-2',
        ownerFieldMetadataId: 'deactivated-field',
        isEnabled: true,
      },
      {
        objectMetadataId: 'deleted-object',
        ownerFieldMetadataId: 'x',
        isEnabled: true,
      },
      {
        objectMetadataId: 'company',
        ownerFieldMetadataId: 'second-owner',
        isEnabled: false,
      },
    ],
    seeAllRoleIds: ['manager'],
  };
  const candidateObjects = [
    company,
    { ...company, objectMetadataId: 'company-2' },
  ];

  it('Save keeps every rule, the invalid ones included, unchanged', () => {
    expect(
      normalizeRowAccessConfig({
        config: brokenConfig,
        roleIds: ['manager', 'member'],
      }),
    ).toEqual(brokenConfig);
  });

  it('lists a warning for each rule the server will refuse, and none for the valid one', () => {
    expect(
      listRowAccessRuleWarnings({
        config: brokenConfig,
        candidateObjects,
        ruleObjects: candidateObjects,
      }),
    ).toEqual([
      {
        objectMetadataId: 'company-2',
        ownerFieldMetadataId: 'deactivated-field',
        isEnabled: true,
        reason: 'owner-field-invalid',
      },
      {
        objectMetadataId: 'deleted-object',
        ownerFieldMetadataId: 'x',
        isEnabled: true,
        reason: 'object-unavailable',
      },
      {
        objectMetadataId: 'company',
        ownerFieldMetadataId: 'second-owner',
        isEnabled: false,
        reason: 'duplicate-object',
      },
    ]);
  });

  it('picking a new owner field clears the warning', () => {
    const fixed = setRuleOwnerField(brokenConfig, 'company-2', 'account-owner');

    expect(
      listRowAccessRuleWarnings({
        config: fixed,
        candidateObjects,
        ruleObjects: candidateObjects,
      }).map(
        (warning) => warning.objectMetadataId,
      ),
    ).toEqual(['deleted-object', 'company']);
  });

  it('a rule goes away only when the admin removes it', () => {
    expect(
      removeRule(brokenConfig, 'deleted-object').rules.map(
        (rule) => rule.objectMetadataId,
      ),
    ).toEqual(['company', 'company-2', 'company']);
  });
});
