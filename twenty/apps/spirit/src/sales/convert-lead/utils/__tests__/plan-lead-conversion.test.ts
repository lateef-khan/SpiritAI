import { describe, expect, it } from 'vitest';

import {
  type LeadToConvert,
  planLeadConversion,
} from 'src/sales/convert-lead/utils/plan-lead-conversion.util';

const REP_MEMBER_ID = '8a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d';
const PERSON_ID = '5d1f3a2b-7c4e-4f8a-b9d0-1e2f3a4b5c6d';
const COMPANY_ID = '2f4e6d8c-0b1a-4c3e-9f5d-7a6b8c9d0e1f';

const qualifiedLead: LeadToConvert = {
  id: '6e7f8a9b-0c1d-4e2f-a3b4-c5d6e7f8a9b0',
  name: 'Northside Gym - 12 treadmills',
  status: 'QUALIFIED',
  opportunityId: null,
  personId: PERSON_ID,
  companyId: COMPANY_ID,
  budget: { amountMicros: 48000000000, currencyCode: 'USD' },
};

describe('planLeadConversion', () => {
  it('makes an opportunity owned by the clicking member with the lead person, company and budget', () => {
    expect(
      planLeadConversion({
        lead: qualifiedLead,
        workspaceMemberId: REP_MEMBER_ID,
      }),
    ).toEqual({
      kind: 'convert',
      opportunity: {
        name: 'Northside Gym - 12 treadmills',
        ownerId: REP_MEMBER_ID,
        pointOfContactId: PERSON_ID,
        companyId: COMPANY_ID,
        amount: { amountMicros: 48000000000, currencyCode: 'USD' },
      },
    });
  });

  it('leaves the amount empty when the lead has no budget amount', () => {
    expect(
      planLeadConversion({
        lead: {
          ...qualifiedLead,
          budget: { amountMicros: null, currencyCode: '' },
        },
        workspaceMemberId: REP_MEMBER_ID,
      }),
    ).toMatchObject({ kind: 'convert', opportunity: { amount: null } });
  });

  it('names the opportunity "Lead" when the lead has no name', () => {
    expect(
      planLeadConversion({
        lead: { ...qualifiedLead, name: '  ' },
        workspaceMemberId: REP_MEMBER_ID,
      }),
    ).toMatchObject({ opportunity: { name: 'Lead' } });
  });

  it.each([
    [
      'status Converted',
      { status: 'CONVERTED' },
      'This lead is already converted.',
    ],
    [
      'an opportunity already linked',
      { opportunityId: '1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d' },
      'This lead is already converted.',
    ],
    [
      'status Disqualified',
      { status: 'DISQUALIFIED' },
      'A disqualified lead cannot be converted.',
    ],
  ])('refuses a lead with %s', (_label, change, reason) => {
    expect(
      planLeadConversion({
        lead: { ...qualifiedLead, ...change },
        workspaceMemberId: REP_MEMBER_ID,
      }),
    ).toEqual({ kind: 'refuse', reason });
  });

  it('refuses a caller that is not a workspace member', () => {
    expect(
      planLeadConversion({ lead: qualifiedLead, workspaceMemberId: null }),
    ).toEqual({
      kind: 'refuse',
      reason: 'Only a workspace member can convert a lead.',
    });
  });
});
