import {
  defineNavigationMenuItem,
  NavigationMenuItemType,
} from 'twenty-sdk/define';

import { SALES_FOLDER_NAVIGATION_MENU_ITEM_UNIVERSAL_IDENTIFIER } from 'src/constants/universal-identifiers';
import {
  LEAD_SOURCE_OBJECT_UNIVERSAL_IDENTIFIER,
  LEAD_SOURCES_NAVIGATION_MENU_ITEM_UNIVERSAL_IDENTIFIER,
} from 'src/sales/lead-source/lead-source-universal-identifiers.constant';

export default defineNavigationMenuItem({
  universalIdentifier: LEAD_SOURCES_NAVIGATION_MENU_ITEM_UNIVERSAL_IDENTIFIER,
  name: 'Lead Sources',
  position: 1,
  type: NavigationMenuItemType.OBJECT,
  targetObjectUniversalIdentifier: LEAD_SOURCE_OBJECT_UNIVERSAL_IDENTIFIER,
  folderUniversalIdentifier: SALES_FOLDER_NAVIGATION_MENU_ITEM_UNIVERSAL_IDENTIFIER,
});
