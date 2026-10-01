import { describe, expect, it } from 'vitest';

import { planMetaIntake } from 'src/sales/meta-intake/utils/plan-meta-intake.util';

const META_LEAD_ID = '0b6c8f0e-3a8d-4c61-9d1e-6f2a5b7c9e10';
const PERSON_ID = '5d1f3a2b-7c4e-4f8a-b9d0-1e2f3a4b5c6d';

// A metaLead as the Meta Leads app saves it after it fetched the lead
const processedMetaLead = {
  name: 'Jane Doe',
  leadgenId: '1234567890',
  status: 'PROCESSED',
  personId: PERSON_ID,
  campaignName: 'Spring Treadmill Sale',
  adsetName: 'Chicago 25-54',
  adName: 'Carousel A',
  formName: 'Treadmill quote',
};

describe('planMetaIntake', () => {
  it('maps a processed Meta lead to a new lead with its person, campaign source and ad', () => {
    expect(planMetaIntake(META_LEAD_ID, processedMetaLead)).toEqual({
      kind: 'create',
      sourceName: 'Spring Treadmill Sale',
      lead: {
        name: 'Jane Doe',
        status: 'NEW',
        personId: PERSON_ID,
        campaign: 'Carousel A',
        sourceRecordId: META_LEAD_ID,
      },
    });
  });

  it('falls back to the form name for the source and the ad set for the campaign', () => {
    const plan = planMetaIntake(META_LEAD_ID, {
      ...processedMetaLead,
      campaignName: '  ',
      adName: null,
    });

    expect(plan).toMatchObject({
      sourceName: 'Treadmill quote',
      lead: { campaign: 'Chicago 25-54' },
    });
  });

  it('uses "Meta" as the source and no campaign when Meta sent none of them', () => {
    const plan = planMetaIntake(META_LEAD_ID, {
      ...processedMetaLead,
      campaignName: null,
      formName: undefined,
      adName: '',
      adsetName: null,
    });

    expect(plan).toMatchObject({ sourceName: 'Meta', lead: { campaign: null } });
  });

  it('names a lead with no name after its leadgen id', () => {
    expect(
      planMetaIntake(META_LEAD_ID, { ...processedMetaLead, name: ' ' }),
    ).toMatchObject({ lead: { name: 'Meta lead 1234567890' } });
  });

  it('skips a Meta lead the Meta app could not fetch yet', () => {
    expect(
      planMetaIntake(META_LEAD_ID, { ...processedMetaLead, status: 'FAILED' }),
    ).toEqual({ kind: 'skip', reason: 'meta lead status is FAILED' });
  });

  it('keys the lead on the Meta lead record id, the same on every event for it', () => {
    const first = planMetaIntake(META_LEAD_ID, processedMetaLead);
    const again = planMetaIntake(META_LEAD_ID, {
      ...processedMetaLead,
      name: 'Jane D.',
    });

    expect(first.kind === 'create' && first.lead.sourceRecordId).toBe(META_LEAD_ID);
    expect(again.kind === 'create' && again.lead.sourceRecordId).toBe(META_LEAD_ID);
  });
});
