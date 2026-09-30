import { defineSettingsFrontComponent } from 'twenty-sdk/define';

import { SETTINGS_FRONT_COMPONENT_UNIVERSAL_IDENTIFIER } from 'src/constants/universal-identifiers';
import { RowAccessSettings } from 'src/front-components/components/RowAccessSettings';

export default defineSettingsFrontComponent({
  universalIdentifier: SETTINGS_FRONT_COMPONENT_UNIVERSAL_IDENTIFIER,
  name: 'row-access-settings',
  description:
    'Pick the owner-only objects, their owner field, and the roles that see every row.',
  component: RowAccessSettings,
});
