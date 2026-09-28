import { type MetadataObject } from 'src/types/metadata-object.type';
import {
  type OwnerCandidateObject,
  type RuleObject,
} from 'src/types/owner-candidate-object.type';
import { isRuleObjectAllowed } from 'src/utils/is-rule-object-allowed.util';

const WORKSPACE_MEMBER_NAME_SINGULAR = 'workspaceMember';

export const listRuleObjects = (
  objects: MetadataObject[],
  workspaceCustomApplicationId: string | undefined,
): RuleObject[] =>
  objects
    .filter(
      (object) =>
        object.isActive &&
        isRuleObjectAllowed(object, workspaceCustomApplicationId),
    )
    .map((object) => ({
      objectMetadataId: object.id,
      label: object.labelSingular,
    }));

// Objects the admin may protect: active, audited (company, opportunity, task
// or a workspace custom object; never a system object), with at least one active
// MANY_TO_ONE relation to workspaceMember. The server checks the same before
// it applies a rule.
export const listOwnerCandidateObjects = (
  objects: MetadataObject[],
  workspaceCustomApplicationId: string | undefined,
): OwnerCandidateObject[] =>
  objects
    .filter(
      (object) =>
        object.isActive &&
        isRuleObjectAllowed(object, workspaceCustomApplicationId),
    )
    .map((object) => ({
      objectMetadataId: object.id,
      label: object.labelSingular,
      ownerFields: object.fieldsList
        .filter(
          (field) =>
            field.isActive !== false &&
            field.type === 'RELATION' &&
            field.relation?.type === 'MANY_TO_ONE' &&
            field.relation.targetObjectMetadata.nameSingular ===
              WORKSPACE_MEMBER_NAME_SINGULAR,
        )
        .map((field) => ({ fieldMetadataId: field.id, label: field.label }))
        .sort((left, right) => left.label.localeCompare(right.label)),
    }))
    .filter((object) => object.ownerFields.length > 0)
    .sort((left, right) => left.label.localeCompare(right.label));
