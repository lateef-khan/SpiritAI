import {
  defineField,
  FieldType,
  RelationType,
  STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS,
} from 'twenty-sdk/define';

import {
  OPPORTUNITY_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
  LEAD_FIELD_UNIVERSAL_IDENTIFIERS,
  LEAD_OBJECT_UNIVERSAL_IDENTIFIER,
} from 'src/sales/lead/lead-universal-identifiers.constant';

export default defineField({
  universalIdentifier: OPPORTUNITY_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
  objectUniversalIdentifier:
    STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.opportunity.universalIdentifier,
  type: FieldType.RELATION,
  name: 'leads',
  label: 'Leads',
  icon: 'IconTarget',
  relationTargetObjectMetadataUniversalIdentifier: LEAD_OBJECT_UNIVERSAL_IDENTIFIER,
  relationTargetFieldMetadataUniversalIdentifier:
    LEAD_FIELD_UNIVERSAL_IDENTIFIERS.opportunity,
  universalSettings: { relationType: RelationType.ONE_TO_MANY },
});
