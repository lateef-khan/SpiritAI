export type OwnerCandidateField = {
  fieldMetadataId: string;
  label: string;
};

// An object that may hold a rule, whether or not it has an owner field.
export type RuleObject = {
  objectMetadataId: string;
  label: string;
};

export type OwnerCandidateObject = {
  objectMetadataId: string;
  label: string;
  ownerFields: OwnerCandidateField[];
};
