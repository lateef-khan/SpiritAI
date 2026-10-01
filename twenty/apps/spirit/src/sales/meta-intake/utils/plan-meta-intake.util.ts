import { LEAD_STATUS } from 'src/sales/lead/lead-options.constant';

// The metaLead fields the intake reads (Meta Leads app, appunite).
export type MetaLeadRecord = {
  name?: string | null;
  leadgenId?: string | null;
  status?: string | null;
  personId?: string | null;
  campaignName?: string | null;
  adsetName?: string | null;
  adName?: string | null;
  formName?: string | null;
};

export type MetaIntakePlan =
  | { kind: 'skip'; reason: string }
  | {
      kind: 'create';
      sourceName: string;
      lead: {
        name: string;
        status: typeof LEAD_STATUS.NEW;
        personId: string | null;
        campaign: string | null;
        sourceRecordId: string;
      };
    };

// The Meta app writes PROCESSED once it has fetched the lead and matched or
// made its Person; before that there is no person and no campaign.
const META_LEAD_PROCESSED_STATUS = 'PROCESSED';
const DEFAULT_SOURCE_NAME = 'Meta';

const firstText = (...values: (string | null | undefined)[]) =>
  values
    .map((value) => (typeof value === 'string' ? value.trim() : ''))
    .find((value) => value.length > 0);

export const planMetaIntake = (
  metaLeadId: string,
  metaLead: MetaLeadRecord,
): MetaIntakePlan => {
  if (metaLead.status !== META_LEAD_PROCESSED_STATUS) {
    return { kind: 'skip', reason: `meta lead status is ${metaLead.status}` };
  }

  return {
    kind: 'create',
    sourceName:
      firstText(metaLead.campaignName, metaLead.formName) ??
      DEFAULT_SOURCE_NAME,
    lead: {
      name:
        firstText(metaLead.name) ??
        `Meta lead ${firstText(metaLead.leadgenId) ?? metaLeadId}`,
      status: LEAD_STATUS.NEW,
      personId: firstText(metaLead.personId) ?? null,
      campaign: firstText(metaLead.adName, metaLead.adsetName) ?? null,
      sourceRecordId: metaLeadId,
    },
  };
};
