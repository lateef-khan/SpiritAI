import { defineLogicFunction } from 'twenty-sdk/define';

import {
  handleMetaLeadEvent,
  type MetaLeadEvent,
} from 'src/sales/meta-intake/handle-meta-lead-event';
import { META_INTAKE_ON_CREATED_LOGIC_FUNCTION_UNIVERSAL_IDENTIFIER } from 'src/sales/meta-intake/meta-intake-universal-identifiers.constant';

export default defineLogicFunction({
  universalIdentifier: META_INTAKE_ON_CREATED_LOGIC_FUNCTION_UNIVERSAL_IDENTIFIER,
  name: 'meta-intake-on-created',
  description: 'Makes a lead from a new Meta lead the Meta app has processed.',
  timeoutSeconds: 30,
  handler: (event: MetaLeadEvent) => handleMetaLeadEvent(event),
  databaseEventTriggerSettings: { eventName: 'metaLead.created' },
});
