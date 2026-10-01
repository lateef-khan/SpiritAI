import {
  defineObject,
  FieldType,
  RelationType,
} from 'twenty-sdk/define';

import { LEAD_SOURCE_TYPE } from 'src/sales/lead-source/lead-source-options.constant';
import {
  LEAD_SOURCE_FIELD_UNIVERSAL_IDENTIFIERS as FIELD_IDS,
  LEAD_SOURCE_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
  LEAD_SOURCE_OBJECT_UNIVERSAL_IDENTIFIER,
} from 'src/sales/lead-source/lead-source-universal-identifiers.constant';
import {
  LEAD_FIELD_UNIVERSAL_IDENTIFIERS,
  LEAD_OBJECT_UNIVERSAL_IDENTIFIER,
} from 'src/sales/lead/lead-universal-identifiers.constant';

export default defineObject({
  universalIdentifier: LEAD_SOURCE_OBJECT_UNIVERSAL_IDENTIFIER,
  nameSingular: 'leadSource',
  namePlural: 'leadSources',
  labelSingular: 'Lead Source',
  labelPlural: 'Lead Sources',
  description: 'Where leads come from: an ad campaign, an event, a referrer',
  icon: 'IconSpeakerphone',
  fields: [
    {
      universalIdentifier: FIELD_IDS.sourceType,
      name: 'sourceType',
      label: 'Type',
      type: FieldType.SELECT,
      icon: 'IconCategory',
      options: [
        { value: LEAD_SOURCE_TYPE.AD, label: 'Ad', position: 0, color: 'blue' },
        { value: LEAD_SOURCE_TYPE.REFERRAL, label: 'Referral', position: 1, color: 'green' },
        { value: LEAD_SOURCE_TYPE.EVENT, label: 'Event', position: 2, color: 'purple' },
        { value: LEAD_SOURCE_TYPE.WEB_CHAT, label: 'Web chat', position: 3, color: 'turquoise' },
        { value: LEAD_SOURCE_TYPE.PHONE, label: 'Phone', position: 4, color: 'orange' },
        { value: LEAD_SOURCE_TYPE.OTHER, label: 'Other', position: 5, color: 'gray' },
      ],
    },
    {
      universalIdentifier: FIELD_IDS.active,
      name: 'active',
      label: 'Active',
      type: FieldType.BOOLEAN,
      icon: 'IconCircleCheck',
      defaultValue: true,
    },
    {
      universalIdentifier: LEAD_SOURCE_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
      name: 'leads',
      label: 'Leads',
      type: FieldType.RELATION,
      icon: 'IconTarget',
      relationTargetObjectMetadataUniversalIdentifier:
        LEAD_OBJECT_UNIVERSAL_IDENTIFIER,
      relationTargetFieldMetadataUniversalIdentifier:
        LEAD_FIELD_UNIVERSAL_IDENTIFIERS.source,
      universalSettings: { relationType: RelationType.ONE_TO_MANY },
    },
  ],
});
