import { defineLogicFunction, type RoutePayload } from 'twenty-sdk/define';
import { Response } from 'twenty-sdk/logic-function';

import {
  CONVERT_LEAD_LOGIC_FUNCTION_UNIVERSAL_IDENTIFIER,
  CONVERT_LEAD_ROUTE_PATH,
} from 'src/sales/convert-lead/convert-lead-universal-identifiers.constant';
import {
  type LeadToConvert,
  planLeadConversion,
} from 'src/sales/convert-lead/utils/plan-lead-conversion.util';
import { LEAD_STATUS } from 'src/sales/lead/lead-options.constant';
import {
  describeGraphqlErrors,
  twentyGraphql,
} from 'src/shared/twenty-graphql';

const jsonResponse = (status: number, body: Record<string, unknown>) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { 'content-type': 'application/json' },
  });

// The token names the clicking user; the member id is what an owner field holds.
const findCallerWorkspaceMemberId = async (): Promise<string | null> => {
  const payload = (process.env.TWENTY_APP_ACCESS_TOKEN ?? '').split('.')[1];
  const userId = payload
    ? (JSON.parse(Buffer.from(payload, 'base64url').toString('utf8'))
        .userId as string | undefined)
    : undefined;

  if (userId === undefined) {
    return null;
  }

  const result = await twentyGraphql<{
    workspaceMembers: { edges: { node: { id: string } }[] };
  }>(
    'person',
    `query CallerMember($userId: UUID!) {
      workspaceMembers(filter: { userId: { eq: $userId } }, first: 1) {
        edges { node { id } }
      }
    }`,
    { userId },
  );

  return result.data?.workspaceMembers.edges[0]?.node.id ?? null;
};

// Everything runs as the clicking user: a lead or opportunity their row rule
// hides stays out of reach.
const handler = async (payload: RoutePayload) => {
  const { leadId } = (payload.body ?? {}) as { leadId?: string };

  if (typeof leadId !== 'string' || leadId.length === 0) {
    return jsonResponse(400, { error: 'leadId is required.' });
  }

  const found = await twentyGraphql<{ lead: LeadToConvert | null }>(
    'person',
    `query LeadToConvert($id: UUID!) {
      lead(filter: { id: { eq: $id } }) {
        id name status opportunityId personId companyId
        budget { amountMicros currencyCode }
      }
    }`,
    { id: leadId },
  );

  if (!found.data?.lead) {
    return jsonResponse(404, { error: 'Lead not found.' });
  }

  const plan = planLeadConversion({
    lead: found.data.lead,
    workspaceMemberId: await findCallerWorkspaceMemberId(),
  });

  if (plan.kind === 'refuse') {
    return jsonResponse(409, { error: plan.reason });
  }

  const created = await twentyGraphql<{ createOpportunity: { id: string } }>(
    'person',
    `mutation OpportunityFromLead($data: OpportunityCreateInput!) {
      createOpportunity(data: $data) { id }
    }`,
    { data: plan.opportunity },
  );
  const opportunityId = created.data?.createOpportunity?.id;

  if (opportunityId === undefined) {
    return jsonResponse(500, {
      error: `Could not create the opportunity: ${describeGraphqlErrors(created.errors)}`,
    });
  }

  const updated = await twentyGraphql<{ updateLead: { id: string } }>(
    'person',
    `mutation MarkLeadConverted($id: UUID!, $data: LeadUpdateInput!) {
      updateLead(id: $id, data: $data) { id }
    }`,
    { id: leadId, data: { status: LEAD_STATUS.CONVERTED, opportunityId } },
  );

  if (!updated.data?.updateLead) {
    return jsonResponse(500, {
      error: `Opportunity ${opportunityId} was created but the lead was not updated: ${describeGraphqlErrors(updated.errors)}`,
    });
  }

  return jsonResponse(200, { opportunityId });
};

export default defineLogicFunction({
  universalIdentifier: CONVERT_LEAD_LOGIC_FUNCTION_UNIVERSAL_IDENTIFIER,
  name: 'convert-lead',
  description:
    'Makes an opportunity owned by the clicking user from a lead and marks the lead converted.',
  timeoutSeconds: 30,
  handler,
  httpRouteTriggerSettings: {
    path: CONVERT_LEAD_ROUTE_PATH,
    httpMethod: 'POST',
    isAuthRequired: true,
  },
});
