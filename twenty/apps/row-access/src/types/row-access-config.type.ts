export type RowAccessRule = {
  objectMetadataId: string;
  ownerFieldMetadataId: string;
  isEnabled: boolean;
};

export type RowAccessConfig = {
  version: 1;
  rules: RowAccessRule[];
  seeAllRoleIds: string[];
};
