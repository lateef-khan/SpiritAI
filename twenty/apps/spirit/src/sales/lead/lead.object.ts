import {
  defineObject,
  FieldType,
  OnDeleteAction,
  RelationType,
  STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS,
} from 'twenty-sdk/define';

import { LEAD_SOURCE_LEADS_FIELD_UNIVERSAL_IDENTIFIER, LEAD_SOURCE_OBJECT_UNIVERSAL_IDENTIFIER } from 'src/sales/lead-source/lead-source-universal-identifiers.constant';
import { LEAD_STATUS } from 'src/sales/lead/lead-options.constant';
import {
  COMPANY_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
  LEAD_FIELD_UNIVERSAL_IDENTIFIERS as FIELD_IDS,
  LEAD_OBJECT_UNIVERSAL_IDENTIFIER,
  OPPORTUNITY_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
  PERSON_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
  WORKSPACE_MEMBER_OWNED_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
} from 'src/sales/lead/lead-universal-identifiers.constant';

const manyToOne = ({
  name,
  label,
  icon,
  targetObjectUniversalIdentifier,
  targetFieldUniversalIdentifier,
}: {
  name: keyof typeof FIELD_IDS;
  label: string;
  icon: string;
  targetObjectUniversalIdentifier: string;
  targetFieldUniversalIdentifier: string;
}) => ({
  universalIdentifier: FIELD_IDS[name],
  name,
  label,
  icon,
  type: FieldType.RELATION as const,
  relationTargetObjectMetadataUniversalIdentifier:
    targetObjectUniversalIdentifier,
  relationTargetFieldMetadataUniversalIdentifier: targetFieldUniversalIdentifier,
  universalSettings: {
    relationType: RelationType.MANY_TO_ONE,
    onDelete: OnDeleteAction.SET_NULL,
    joinColumnName: `${name}Id`,
  },
});

export default defineObject({
  universalIdentifier: LEAD_OBJECT_UNIVERSAL_IDENTIFIER,
  nameSingular: 'lead',
  namePlural: 'leads',
  labelSingular: 'Lead',
  labelPlural: 'Leads',
  description: 'A possible sale, before it becomes an opportunity',
  icon: 'IconTarget',
  fields: [
    {
      universalIdentifier: FIELD_IDS.status,
      name: 'status',
      label: 'Status',
      type: FieldType.SELECT,
      icon: 'IconProgressCheck',
      defaultValue: `'${LEAD_STATUS.NEW}'`,
      options: [
        { value: LEAD_STATUS.NEW, label: 'New', position: 0, color: 'gray' },
        { value: LEAD_STATUS.CONTACTED, label: 'Contacted', position: 1, color: 'blue' },
        { value: LEAD_STATUS.QUALIFIED, label: 'Qualified', position: 2, color: 'purple' },
        { value: LEAD_STATUS.CONVERTED, label: 'Converted', position: 3, color: 'green' },
        { value: LEAD_STATUS.DISQUALIFIED, label: 'Disqualified', position: 4, color: 'red' },
      ],
    },
    manyToOne({
      name: 'owner',
      label: 'Owner',
      icon: 'IconUserCircle',
      targetObjectUniversalIdentifier:
        STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.workspaceMember.universalIdentifier,
      targetFieldUniversalIdentifier:
        WORKSPACE_MEMBER_OWNED_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
    }),
    manyToOne({
      name: 'source',
      label: 'Source',
      icon: 'IconSpeakerphone',
      targetObjectUniversalIdentifier: LEAD_SOURCE_OBJECT_UNIVERSAL_IDENTIFIER,
      targetFieldUniversalIdentifier: LEAD_SOURCE_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
    }),
    {
      universalIdentifier: FIELD_IDS.campaign,
      name: 'campaign',
      label: 'Campaign',
      type: FieldType.TEXT,
      icon: 'IconAd',
    },
    {
      universalIdentifier: FIELD_IDS.buyerType,
      name: 'buyerType',
      label: 'Buyer type',
      type: FieldType.SELECT,
      icon: 'IconBuildingStore',
      options: [
        { value: 'COMMERCIAL', label: 'Commercial', position: 0, color: 'blue' },
        { value: 'HOME', label: 'Home', position: 1, color: 'green' },
      ],
    },
    {
      universalIdentifier: FIELD_IDS.interest,
      name: 'interest',
      label: 'Interest',
      type: FieldType.MULTI_SELECT,
      icon: 'IconBarbell',
      options: [
        { value: 'TREADMILL', label: 'Treadmill', position: 0, color: 'blue' },
        { value: 'BIKE', label: 'Bike', position: 1, color: 'green' },
        { value: 'ELLIPTICAL', label: 'Elliptical', position: 2, color: 'purple' },
        { value: 'STRENGTH', label: 'Strength', position: 3, color: 'orange' },
        { value: 'OTHER', label: 'Other', position: 4, color: 'gray' },
      ],
    },
    {
      universalIdentifier: FIELD_IDS.quantity,
      name: 'quantity',
      label: 'Quantity',
      type: FieldType.NUMBER,
      icon: 'IconHash',
    },
    {
      universalIdentifier: FIELD_IDS.budget,
      name: 'budget',
      label: 'Budget',
      type: FieldType.CURRENCY,
      icon: 'IconCurrencyDollar',
    },
    {
      universalIdentifier: FIELD_IDS.timeline,
      name: 'timeline',
      label: 'Timeline',
      type: FieldType.SELECT,
      icon: 'IconCalendarTime',
      options: [
        { value: 'NOW', label: 'Now', position: 0, color: 'red' },
        { value: 'ONE_TO_THREE_MONTHS', label: '1–3 months', position: 1, color: 'orange' },
        { value: 'THREE_TO_SIX_MONTHS', label: '3–6 months', position: 2, color: 'yellow' },
        { value: 'LATER', label: 'Later', position: 3, color: 'gray' },
      ],
    },
    {
      universalIdentifier: FIELD_IDS.siteAddress,
      name: 'siteAddress',
      label: 'Address',
      type: FieldType.ADDRESS,
      icon: 'IconMap',
    },
    {
      universalIdentifier: FIELD_IDS.disqualifyReason,
      name: 'disqualifyReason',
      label: 'Disqualify reason',
      type: FieldType.SELECT,
      icon: 'IconBan',
      options: [
        { value: 'NO_BUDGET', label: 'No budget', position: 0, color: 'orange' },
        { value: 'WRONG_FIT', label: 'Wrong fit', position: 1, color: 'purple' },
        { value: 'NO_REPLY', label: 'No reply', position: 2, color: 'gray' },
        { value: 'DUPLICATE', label: 'Duplicate', position: 3, color: 'blue' },
        { value: 'SPAM', label: 'Spam', position: 4, color: 'red' },
      ],
    },
    {
      universalIdentifier: FIELD_IDS.firstReplyAt,
      name: 'firstReplyAt',
      label: 'First reply at',
      type: FieldType.DATE_TIME,
      icon: 'IconClock',
    },
    manyToOne({
      name: 'person',
      label: 'Person',
      icon: 'IconUser',
      targetObjectUniversalIdentifier:
        STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.person.universalIdentifier,
      targetFieldUniversalIdentifier: PERSON_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
    }),
    manyToOne({
      name: 'company',
      label: 'Company',
      icon: 'IconBuildingSkyscraper',
      targetObjectUniversalIdentifier:
        STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.company.universalIdentifier,
      targetFieldUniversalIdentifier: COMPANY_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
    }),
    manyToOne({
      name: 'opportunity',
      label: 'Opportunity',
      icon: 'IconTargetArrow',
      targetObjectUniversalIdentifier:
        STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.opportunity.universalIdentifier,
      targetFieldUniversalIdentifier:
        OPPORTUNITY_LEADS_FIELD_UNIVERSAL_IDENTIFIER,
    }),
    {
      universalIdentifier: FIELD_IDS.sourceRecordId,
      name: 'sourceRecordId',
      label: 'Source record id',
      type: FieldType.UUID,
      icon: 'IconLink',
      isUnique: true,
    },
  ],
});
