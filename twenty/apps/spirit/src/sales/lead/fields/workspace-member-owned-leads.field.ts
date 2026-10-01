import {
  defineField,
  FieldType,
  RelationType,
  STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS,
} from 'twenty-sdk/define';

import {
  WORKSPACE_MEMBER_OWNED_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
  LEAD_FIELD_UNIVERSAL_IDENTIFIERS,
  LEAD_OBJECT_UNIVERSAL_IDENTIFIER,
} from 'src/sales/lead/lead-universal-identifiers.constant';

export default defineField({
  universalIdentifier: WORKSPACE_MEMBER_OWNED_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
  objectUniversalIdentifier:
    STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.workspaceMember.universalIdentifier,
  type: FieldType.RELATION,
  name: 'ownedLeads',
  label: 'Owned leads',
  icon: 'IconTarget',
  relationTargetObjectMetadataUniversalIdentifier: LEAD_OBJECT_UNIVERSAL_IDENTIFIER,
  relationTargetFieldMetadataUniversalIdentifier:
    LEAD_FIELD_UNIVERSAL_IDENTIFIERS.owner,
  universalSettings: { relationType: RelationType.ONE_TO_MANY },
});
