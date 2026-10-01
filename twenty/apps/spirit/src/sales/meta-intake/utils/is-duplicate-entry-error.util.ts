import { type GraphqlError } from 'src/shared/twenty-graphql';

// Twenty's answer when a create hits a unique field, here lead.sourceRecordId.
const DUPLICATE_ENTRY_MESSAGE_START = 'A duplicate entry was detected';

export const isDuplicateEntryError = (errors: GraphqlError[] | undefined) =>
  (errors ?? []).some(
    (error) =>
      error.extensions?.code === 'BAD_USER_INPUT' &&
      error.message.startsWith(DUPLICATE_ENTRY_MESSAGE_START),
  );
