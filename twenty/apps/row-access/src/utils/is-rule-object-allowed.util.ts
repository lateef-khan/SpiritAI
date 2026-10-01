import { STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS } from 'src/constants/standard-object-universal-identifiers.constant';
import { type MetadataObject } from 'src/types/metadata-object.type';

const AUDITED_STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS = new Set<string>([
  STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.company,
  STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.opportunity,
  STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.task,
]);

// The same definition as the server validator (design D32, D41): a rule may
// sit on company, opportunity, task, an object of the workspace's own custom
// application, or an object of one of our own apps. Objects of other
// installed apps, other standard objects and every system object are refused
// by the server, so the page does not offer them. Without a known custom
// application only the audited standard objects and our own apps' objects
// are offered.
export const isRuleObjectAllowed = (
  object: Pick<
    MetadataObject,
    'universalIdentifier' | 'isSystem' | 'applicationId'
  >,
  workspaceCustomApplicationId: string | undefined,
  ownApplicationIds: string[] = [],
): boolean => {
  if (
    object.isSystem ||
    object.universalIdentifier ===
      STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.workspaceMember
  ) {
    return false;
  }

  if (AUDITED_STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.has(object.universalIdentifier)) {
    return true;
  }

  if (ownApplicationIds.includes(object.applicationId)) {
    return true;
  }

  return (
    workspaceCustomApplicationId !== undefined &&
    object.applicationId === workspaceCustomApplicationId
  );
};
