import { STANDARD_OBJECT } from 'twenty-sdk/define';
import { describe, expect, it } from 'vitest';

import { STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS } from 'src/constants/standard-object-universal-identifiers.constant';
import { type MetadataObject } from 'src/types/metadata-object.type';
import {
  listOwnerCandidateObjects,
  listRuleObjects,
} from 'src/utils/list-owner-candidate-objects.util';
import { listRowAccessRuleWarnings } from 'src/utils/update-row-access-config.util';

const CUSTOM_APPLICATION_ID = 'workspace-custom-application';

const toWorkspaceMember = {
  type: 'MANY_TO_ONE',
  targetObjectMetadata: { nameSingular: 'workspaceMember' },
};

const company: MetadataObject = {
  id: 'company',
  universalIdentifier: STANDARD_OBJECT.company.universalIdentifier,
  applicationId: 'standard-application',
  nameSingular: 'company',
  labelSingular: 'Company',
  isSystem: false,
  isActive: true,
  fieldsList: [
    { id: 'name', name: 'name', label: 'Name', type: 'TEXT' },
    {
      id: 'account-owner',
      name: 'accountOwner',
      label: 'Account Owner',
      type: 'RELATION',
      isActive: true,
      relation: toWorkspaceMember,
    },
    {
      id: 'people',
      name: 'people',
      label: 'People',
      type: 'RELATION',
      relation: {
        type: 'ONE_TO_MANY',
        targetObjectMetadata: { nameSingular: 'person' },
      },
    },
  ],
};

describe('listOwnerCandidateObjects', () => {
  it('offers company with its accountOwner field', () => {
    expect(listOwnerCandidateObjects([company], CUSTOM_APPLICATION_ID)).toEqual([
      {
        objectMetadataId: 'company',
        label: 'Company',
        ownerFields: [{ fieldMetadataId: 'account-owner', label: 'Account Owner' }],
      },
    ]);
  });

  it('skips system objects, workspaceMember, inactive objects and objects with no owner relation', () => {
    expect(
      listOwnerCandidateObjects([
        { ...company, id: 'connected-account', isSystem: true },
        {
          ...company,
          id: 'member',
          nameSingular: 'workspaceMember',
          universalIdentifier: STANDARD_OBJECT.workspaceMember.universalIdentifier,
          isSystem: true,
        },
        { ...company, id: 'inactive', isActive: false },
        {
          ...company,
          id: 'person',
          fieldsList: [company.fieldsList[0], company.fieldsList[2]],
        },
      ], CUSTOM_APPLICATION_ID),
    ).toEqual([]);
  });

  it('skips a ONE_TO_MANY or an inactive relation to workspaceMember', () => {
    const [candidate] = listOwnerCandidateObjects([
      {
        ...company,
        fieldsList: [
          ...company.fieldsList,
          {
            id: 'inverse',
            name: 'inverse',
            label: 'Inverse',
            type: 'RELATION',
            relation: { ...toWorkspaceMember, type: 'ONE_TO_MANY' },
          },
          {
            id: 'old-owner',
            name: 'oldOwner',
            label: 'Old owner',
            type: 'RELATION',
            isActive: false,
            relation: toWorkspaceMember,
          },
        ],
      },
    ], CUSTOM_APPLICATION_ID);

    expect(candidate.ownerFields.map((field) => field.fieldMetadataId)).toEqual([
      'account-owner',
    ]);
  });
});

// D32, D41: only audited objects can hold a rule: company, opportunity, task
// and objects of the workspace's own custom application.
describe('listOwnerCandidateObjects: audited objects only', () => {
  const withOwner = (
    id: string,
    universalIdentifier: string,
    label: string,
    isSystem = false,
    applicationId = 'standard-application',
  ): MetadataObject => ({
    ...company,
    id,
    universalIdentifier,
    applicationId,
    nameSingular: id,
    labelSingular: label,
    isSystem,
  });

  it('offers company, opportunity, task and a workspace custom object; not person, a system object, another standard object or an object of another app', () => {
    expect(
      listOwnerCandidateObjects([
        withOwner('company', STANDARD_OBJECT.company.universalIdentifier, 'Company'),
        withOwner(
          'opportunity',
          STANDARD_OBJECT.opportunity.universalIdentifier,
          'Opportunity',
        ),
        withOwner('task', STANDARD_OBJECT.task.universalIdentifier, 'Task'),
        withOwner(
          'rocket',
          'custom-rocket-universal-identifier',
          'Rocket',
          false,
          CUSTOM_APPLICATION_ID,
        ),
        withOwner(
          'invoice',
          'other-app-invoice-universal-identifier',
          'Invoice',
          false,
          'other-installed-application',
        ),
        withOwner('person', STANDARD_OBJECT.person.universalIdentifier, 'Person'),
        withOwner('workflow', STANDARD_OBJECT.workflow.universalIdentifier, 'Workflow'),
        withOwner(
          'timelineActivity',
          STANDARD_OBJECT.timelineActivity.universalIdentifier,
          'Timeline activity',
          true,
        ),
        withOwner(
          'customSystem',
          'custom-system-universal-identifier',
          'Custom system',
          true,
          CUSTOM_APPLICATION_ID,
        ),
      ], CUSTOM_APPLICATION_ID).map((candidate) => candidate.objectMetadataId),
    ).toEqual(['company', 'opportunity', 'rocket', 'task']);
  });

  it("offers an object of our own app, and not the same object when its app is not one of ours", () => {
    const objects = [
      withOwner('task', STANDARD_OBJECT.task.universalIdentifier, 'Task'),
      withOwner(
        'lead',
        'spirit-lead-universal-identifier',
        'Lead',
        false,
        'spirit-application',
      ),
    ];

    expect(
      listOwnerCandidateObjects(objects, CUSTOM_APPLICATION_ID, [
        'spirit-application',
      ]).map((candidate) => candidate.objectMetadataId),
    ).toEqual(['lead', 'task']);
    expect(
      listOwnerCandidateObjects(objects, CUSTOM_APPLICATION_ID, [
        'another-application',
      ]).map((candidate) => candidate.objectMetadataId),
    ).toEqual(['task']);
  });

  it('offers only the audited standard objects when the workspace custom application is unknown', () => {
    expect(
      listOwnerCandidateObjects([
        withOwner('task', STANDARD_OBJECT.task.universalIdentifier, 'Task'),
        withOwner(
          'rocket',
          'custom-rocket-universal-identifier',
          'Rocket',
          false,
          CUSTOM_APPLICATION_ID,
        ),
      ], undefined).map((candidate) => candidate.objectMetadataId),
    ).toEqual(['task']);
  });

  it('the literal universal identifiers match STANDARD_OBJECT', () => {
    expect(STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS).toEqual({
      company: STANDARD_OBJECT.company.universalIdentifier,
      opportunity: STANDARD_OBJECT.opportunity.universalIdentifier,
      task: STANDARD_OBJECT.task.universalIdentifier,
      workspaceMember: STANDARD_OBJECT.workspaceMember.universalIdentifier,
    });
  });
});

describe('rule warnings for an object with no owner field left', () => {
  it('names the object and blames its owner field, not the object', () => {
    const companyWithOwnerDeactivated: MetadataObject = {
      ...company,
      fieldsList: company.fieldsList.map((field) =>
        field.id === 'account-owner' ? { ...field, isActive: false } : field,
      ),
    };
    const objects = [companyWithOwnerDeactivated];
    const ruleObjects = listRuleObjects(objects, CUSTOM_APPLICATION_ID);

    const warnings = listRowAccessRuleWarnings({
      config: {
        version: 1,
        rules: [
          {
            objectMetadataId: 'company',
            ownerFieldMetadataId: 'account-owner',
            isEnabled: true,
          },
        ],
        seeAllRoleIds: [],
      },
      candidateObjects: listOwnerCandidateObjects(
        objects,
        CUSTOM_APPLICATION_ID,
      ),
      ruleObjects,
    });

    expect(warnings.map((warning) => warning.reason)).toEqual([
      'owner-field-invalid',
    ]);
    expect(
      ruleObjects.find(
        (object) => object.objectMetadataId === warnings[0].objectMetadataId,
      )?.label,
    ).toBe('Company');
  });
});
