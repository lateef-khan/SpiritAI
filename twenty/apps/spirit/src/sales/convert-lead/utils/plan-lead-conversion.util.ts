import { LEAD_STATUS } from 'src/sales/lead/lead-options.constant';

export type Currency = {
  amountMicros: number | null;
  currencyCode: string | null;
};

export type LeadToConvert = {
  id: string;
  name: string | null;
  status: string | null;
  opportunityId: string | null;
  personId: string | null;
  companyId: string | null;
  budget: Currency | null;
};

export type OpportunityInput = {
  name: string;
  ownerId: string;
  pointOfContactId: string | null;
  companyId: string | null;
  amount: Currency | null;
};

export type LeadConversionPlan =
  | { kind: 'refuse'; reason: string }
  | { kind: 'convert'; opportunity: OpportunityInput };

export const planLeadConversion = ({
  lead,
  workspaceMemberId,
}: {
  lead: LeadToConvert;
  workspaceMemberId: string | null;
}): LeadConversionPlan => {
  if (workspaceMemberId === null) {
    return { kind: 'refuse', reason: 'Only a workspace member can convert a lead.' };
  }

  if (lead.status === LEAD_STATUS.CONVERTED || lead.opportunityId !== null) {
    return { kind: 'refuse', reason: 'This lead is already converted.' };
  }

  if (lead.status === LEAD_STATUS.DISQUALIFIED) {
    return { kind: 'refuse', reason: 'A disqualified lead cannot be converted.' };
  }

  const name = lead.name?.trim();

  return {
    kind: 'convert',
    opportunity: {
      name: name !== undefined && name.length > 0 ? name : 'Lead',
      ownerId: workspaceMemberId,
      pointOfContactId: lead.personId,
      companyId: lead.companyId,
      amount:
        lead.budget !== null && lead.budget.amountMicros !== null
          ? lead.budget
          : null,
    },
  };
};
