// The slice of the metadata API the settings page reads.
export type MetadataField = {
  id: string;
  name: string;
  label: string;
  type: string;
  isActive?: boolean | null;
  relation?: {
    type: string;
    targetObjectMetadata: { nameSingular: string };
  } | null;
};

export type MetadataObject = {
  id: string;
  universalIdentifier: string;
  applicationId: string;
  nameSingular: string;
  labelSingular: string;
  isSystem: boolean;
  isActive: boolean;
  fieldsList: MetadataField[];
};

export type MetadataRole = {
  id: string;
  label: string;
};
