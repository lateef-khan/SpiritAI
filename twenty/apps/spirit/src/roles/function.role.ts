import {
  defineApplicationRole,
  STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS,
} from 'twenty-sdk/define';

import {
  APP_DISPLAY_NAME,
  FUNCTION_ROLE_UNIVERSAL_IDENTIFIER,
} from 'src/constants/universal-identifiers';
import { LEAD_SOURCE_OBJECT_UNIVERSAL_IDENTIFIER } from 'src/sales/lead-source/lead-source-universal-identifiers.constant';
import { META_LEAD_OBJECT_UNIVERSAL_IDENTIFIER } from 'src/sales/meta-intake/meta-intake-universal-identifiers.constant';
import { LEAD_OBJECT_UNIVERSAL_IDENTIFIER } from 'src/sales/lead/lead-universal-identifiers.constant';

const readAndWrite = (objectUniversalIdentifier: string) => ({
  objectUniversalIdentifier,
  canReadObjectRecords: true,
  canUpdateObjectRecords: true,
  canSoftDeleteObjectRecords: false,
  canDestroyObjectRecords: false,
});

const readOnly = (objectUniversalIdentifier: string) => ({
  objectUniversalIdentifier,
  canReadObjectRecords: true,
  canUpdateObjectRecords: false,
  canSoftDeleteObjectRecords: false,
  canDestroyObjectRecords: false,
});

export default defineApplicationRole({
  universalIdentifier: FUNCTION_ROLE_UNIVERSAL_IDENTIFIER,
  label: `${APP_DISPLAY_NAME} function role`,
  description: 'Makes leads and lead sources, converts leads to opportunities.',
  canReadAllObjectRecords: false,
  canUpdateAllObjectRecords: false,
  canSoftDeleteAllObjectRecords: false,
  canDestroyAllObjectRecords: false,
  canUpdateAllSettings: false,
  canAccessAllTools: false,
  canBeAssignedToUsers: false,
  canBeAssignedToAgents: false,
  canBeAssignedToApiKeys: false,
  objectPermissions: [
    readAndWrite(LEAD_OBJECT_UNIVERSAL_IDENTIFIER),
    readAndWrite(LEAD_SOURCE_OBJECT_UNIVERSAL_IDENTIFIER),
    readAndWrite(
      STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.opportunity.universalIdentifier,
    ),
    readOnly(STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.person.universalIdentifier),
    readOnly(STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.company.universalIdentifier),
    readOnly(
      STANDARD_OBJECT_UNIVERSAL_IDENTIFIERS.workspaceMember.universalIdentifier,
    ),
    readOnly(META_LEAD_OBJECT_UNIVERSAL_IDENTIFIER),
  ],
  fieldPermissions: [],
  permissionFlagUniversalIdentifiers: [],
});
