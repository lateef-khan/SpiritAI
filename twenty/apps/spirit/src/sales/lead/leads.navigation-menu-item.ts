import {
  defineNavigationMenuItem,
  NavigationMenuItemType,
} from 'twenty-sdk/define';

import { SALES_FOLDER_NAVIGATION_MENU_ITEM_UNIVERSAL_IDENTIFIER } from 'src/constants/universal-identifiers';
import {
  LEAD_OBJECT_UNIVERSAL_IDENTIFIER,
  LEADS_NAVIGATION_MENU_ITEM_UNIVERSAL_IDENTIFIER,
} from 'src/sales/lead/lead-universal-identifiers.constant';

export default defineNavigationMenuItem({
  universalIdentifier: LEADS_NAVIGATION_MENU_ITEM_UNIVERSAL_IDENTIFIER,
  name: 'Leads',
  position: 0,
  type: NavigationMenuItemType.OBJECT,
  targetObjectUniversalIdentifier: LEAD_OBJECT_UNIVERSAL_IDENTIFIER,
  folderUniversalIdentifier: SALES_FOLDER_NAVIGATION_MENU_ITEM_UNIVERSAL_IDENTIFIER,
});
