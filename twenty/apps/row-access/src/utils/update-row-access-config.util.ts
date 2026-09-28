import { type RowAccessConfig } from 'src/types/row-access-config.type';
import {
  type OwnerCandidateObject,
  type RuleObject,
} from 'src/types/owner-candidate-object.type';

export const setRuleEnabled = (
  config: RowAccessConfig,
  object: OwnerCandidateObject,
  isEnabled: boolean,
): RowAccessConfig => {
  const existingRule = config.rules.find(
    (rule) => rule.objectMetadataId === object.objectMetadataId,
  );

  if (existingRule !== undefined) {
    return {
      ...config,
      rules: config.rules.map((rule) =>
        rule === existingRule ? { ...rule, isEnabled } : rule,
      ),
    };
  }

  const [firstOwnerField] = object.ownerFields;

  if (firstOwnerField === undefined) {
    return config;
  }

  return {
    ...config,
    rules: [
      ...config.rules,
      {
        objectMetadataId: object.objectMetadataId,
        ownerFieldMetadataId: firstOwnerField.fieldMetadataId,
        isEnabled,
      },
    ],
  };
};

export const setRuleOwnerField = (
  config: RowAccessConfig,
  objectMetadataId: string,
  ownerFieldMetadataId: string,
): RowAccessConfig => ({
  ...config,
  rules: config.rules.map((rule) =>
    rule.objectMetadataId === objectMetadataId
      ? { ...rule, ownerFieldMetadataId }
      : rule,
  ),
});

export const setSeeAllRole = (
  config: RowAccessConfig,
  roleId: string,
  isSeeAll: boolean,
): RowAccessConfig => ({
  ...config,
  seeAllRoleIds: isSeeAll
    ? [...new Set([...config.seeAllRoleIds, roleId])]
    : config.seeAllRoleIds.filter((seeAllRoleId) => seeAllRoleId !== roleId),
});

export const removeRule = (
  config: RowAccessConfig,
  objectMetadataId: string,
): RowAccessConfig => ({
  ...config,
  rules: config.rules.filter(
    (rule) => rule.objectMetadataId !== objectMetadataId,
  ),
});

export type RowAccessRuleWarning = {
  objectMetadataId: string;
  ownerFieldMetadataId: string;
  isEnabled: boolean;
  reason: 'object-unavailable' | 'owner-field-invalid' | 'duplicate-object';
};

// Rules the server will refuse: the object is gone, inactive or not allowed,
// the owner field is no longer an active MANY_TO_ONE to workspaceMember, or
// the object holds two rules. An enabled one of these makes the server fail
// closed until the admin fixes or removes it. An object that can hold a rule
// but has no owner field left is not a candidate, yet its owner field is the
// problem, not the object.
export const listRowAccessRuleWarnings = ({
  config,
  candidateObjects,
  ruleObjects,
}: {
  config: RowAccessConfig;
  candidateObjects: OwnerCandidateObject[];
  ruleObjects: RuleObject[];
}): RowAccessRuleWarning[] => {
  const seenObjectIds = new Set<string>();

  return config.rules.flatMap((rule): RowAccessRuleWarning[] => {
    const isDuplicate = seenObjectIds.has(rule.objectMetadataId);

    seenObjectIds.add(rule.objectMetadataId);

    const candidate = candidateObjects.find(
      (object) => object.objectMetadataId === rule.objectMetadataId,
    );

    const reason: RowAccessRuleWarning['reason'] | undefined = isDuplicate
      ? 'duplicate-object'
      : candidate === undefined
        ? ruleObjects.some(
            (object) => object.objectMetadataId === rule.objectMetadataId,
          )
          ? 'owner-field-invalid'
          : 'object-unavailable'
        : !candidate.ownerFields.some(
              (field) => field.fieldMetadataId === rule.ownerFieldMetadataId,
            )
          ? 'owner-field-invalid'
          : undefined;

    return reason === undefined ? [] : [{ ...rule, reason }];
  });
};

// What the page saves. Every rule is kept as it is, including one whose
// object or owner field became invalid: dropping it would open every row of
// that object, while keeping it makes the server fail closed until the admin
// fixes or removes it (the page lists it as a warning). Roles that no longer
// exist are dropped, since the server ignores them anyway.
export const normalizeRowAccessConfig = ({
  config,
  roleIds,
}: {
  config: RowAccessConfig;
  roleIds: string[];
}): RowAccessConfig => ({
  version: 1,
  rules: config.rules.map((rule) => ({ ...rule })),
  seeAllRoleIds: [...new Set(config.seeAllRoleIds)].filter((roleId) =>
    roleIds.includes(roleId),
  ),
});
