import { defineLogicFunction } from 'twenty-sdk/define';

import {
  handleMetaLeadEvent,
  type MetaLeadEvent,
} from 'src/sales/meta-intake/handle-meta-lead-event';
import { META_INTAKE_ON_STATUS_CHANGE_LOGIC_FUNCTION_UNIVERSAL_IDENTIFIER } from 'src/sales/meta-intake/meta-intake-universal-identifiers.constant';

// The Meta app first saves a lead it cannot fetch as FAILED and later
// updates it to PROCESSED, which is not a create.
export default defineLogicFunction({
  universalIdentifier:
    META_INTAKE_ON_STATUS_CHANGE_LOGIC_FUNCTION_UNIVERSAL_IDENTIFIER,
  name: 'meta-intake-on-status-change',
  description: 'Makes a lead when a failed Meta lead is processed later.',
  timeoutSeconds: 30,
  handler: (event: MetaLeadEvent) => handleMetaLeadEvent(event),
  databaseEventTriggerSettings: {
    eventName: 'metaLead.updated',
    updatedFields: ['status'],
  },
});
