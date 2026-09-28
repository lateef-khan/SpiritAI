import { type RowAccessConfig } from 'src/types/row-access-config.type';

// Installed value: the app is present but no object is filtered yet. The
// server fails closed only when the app or this value is missing or invalid.
export const EMPTY_ROW_ACCESS_CONFIG: RowAccessConfig = {
  version: 1,
  rules: [],
  seeAllRoleIds: [],
};
