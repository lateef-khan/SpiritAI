import { LEAD_SOURCE_TYPE } from 'src/sales/lead-source/lead-source-options.constant';
import { isDuplicateEntryError } from 'src/sales/meta-intake/utils/is-duplicate-entry-error.util';
import {
  type MetaLeadRecord,
  planMetaIntake,
} from 'src/sales/meta-intake/utils/plan-meta-intake.util';
import {
  describeGraphqlErrors,
  twentyGraphql,
} from 'src/shared/twenty-graphql';

export type MetaLeadEvent = {
  recordId: string;
  properties?: { after?: MetaLeadRecord };
};

export type MetaIntakeResult =
  | { kind: 'skipped'; reason: string }
  | { kind: 'created'; leadId: string }
  | { kind: 'already-exists' };

// Runs as the app alone: nobody owns the new lead until someone assigns it,
// whoever made the Meta lead.
const findOrCreateAdSource = async (name: string): Promise<string> => {
  const found = await twentyGraphql<{
    leadSources: { edges: { node: { id: string } }[] };
  }>(
    'application',
    `query FindAdSource($name: String!) {
      leadSources(filter: { name: { eq: $name }, sourceType: { eq: AD } }, first: 1) {
        edges { node { id } }
      }
    }`,
    { name },
  );
  const foundId = found.data?.leadSources.edges[0]?.node.id;

  if (foundId !== undefined) {
    return foundId;
  }

  const created = await twentyGraphql<{ createLeadSource: { id: string } }>(
    'application',
    `mutation CreateAdSource($data: LeadSourceCreateInput!) {
      createLeadSource(data: $data) { id }
    }`,
    { data: { name, sourceType: LEAD_SOURCE_TYPE.AD, active: true } },
  );
  const createdId = created.data?.createLeadSource?.id;

  if (createdId === undefined) {
    throw new Error(
      `Could not create lead source "${name}": ${describeGraphqlErrors(created.errors)}`,
    );
  }

  return createdId;
};

export const handleMetaLeadEvent = async (
  event: MetaLeadEvent,
): Promise<MetaIntakeResult> => {
  const plan = planMetaIntake(event.recordId, event.properties?.after ?? {});

  if (plan.kind === 'skip') {
    return { kind: 'skipped', reason: plan.reason };
  }

  const sourceId = await findOrCreateAdSource(plan.sourceName);

  const created = await twentyGraphql<{ createLead: { id: string } }>(
    'application',
    `mutation CreateLeadFromMeta($data: LeadCreateInput!) {
      createLead(data: $data) { id }
    }`,
    { data: { ...plan.lead, sourceId } },
  );
  const leadId = created.data?.createLead?.id;

  if (leadId !== undefined) {
    return { kind: 'created', leadId };
  }

  if (isDuplicateEntryError(created.errors)) {
    return { kind: 'already-exists' };
  }

  throw new Error(
    `Could not create the lead for meta lead ${event.recordId}: ${describeGraphqlErrors(created.errors)}`,
  );
};
