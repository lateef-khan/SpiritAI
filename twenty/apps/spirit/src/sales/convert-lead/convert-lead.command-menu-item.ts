import { defineCommandMenuItem } from 'twenty-sdk/define';

import {
  CONVERT_LEAD_COMMAND_MENU_ITEM_UNIVERSAL_IDENTIFIER,
  CONVERT_LEAD_FRONT_COMPONENT_UNIVERSAL_IDENTIFIER,
} from 'src/sales/convert-lead/convert-lead-universal-identifiers.constant';
import { LEAD_OBJECT_UNIVERSAL_IDENTIFIER } from 'src/sales/lead/lead-universal-identifiers.constant';

export default defineCommandMenuItem({
  universalIdentifier: CONVERT_LEAD_COMMAND_MENU_ITEM_UNIVERSAL_IDENTIFIER,
  label: 'Convert to opportunity',
  shortLabel: 'Convert',
  isPinned: true,
  availabilityType: 'RECORD_SELECTION',
  availabilityObjectUniversalIdentifier: LEAD_OBJECT_UNIVERSAL_IDENTIFIER,
  frontComponentUniversalIdentifier:
    CONVERT_LEAD_FRONT_COMPONENT_UNIVERSAL_IDENTIFIER,
});
