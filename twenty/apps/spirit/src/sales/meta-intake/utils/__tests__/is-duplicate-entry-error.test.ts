import { describe, expect, it } from 'vitest';

import { isDuplicateEntryError } from 'src/sales/meta-intake/utils/is-duplicate-entry-error.util';

// The error Twenty 2.41 returns for createLead with a sourceRecordId that
// another lead already holds (seen on the local server)
const DUPLICATE_META_LEAD_ID_ERROR = {
  message: 'A duplicate entry was detected',
  extensions: {
    code: 'BAD_USER_INPUT',
    userFriendlyMessage:
      'This record already exists. Please check your data and try again.',
  },
};

describe('isDuplicateEntryError', () => {
  it('is true for the duplicate entry error', () => {
    expect(isDuplicateEntryError([DUPLICATE_META_LEAD_ID_ERROR])).toBe(true);
  });

  it('is false for another bad input error', () => {
    expect(
      isDuplicateEntryError([
        {
          message: 'Invalid UUID format',
          extensions: { code: 'BAD_USER_INPUT' },
        },
      ]),
    ).toBe(false);
  });

  it('is false for a permission error with a similar message', () => {
    expect(
      isDuplicateEntryError([
        {
          message: 'A duplicate entry was detected',
          extensions: { code: 'FORBIDDEN' },
        },
      ]),
    ).toBe(false);
  });

  it('is false when there is no error', () => {
    expect(isDuplicateEntryError(undefined)).toBe(false);
  });
});
