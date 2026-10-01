import { RestApiClient } from 'twenty-client-sdk/rest';
import { defineFrontComponent } from 'twenty-sdk/define';
import { Command, useRecordId } from 'twenty-sdk/front-component';

import {
  CONVERT_LEAD_FRONT_COMPONENT_UNIVERSAL_IDENTIFIER,
  CONVERT_LEAD_ROUTE_PATH,
} from 'src/sales/convert-lead/convert-lead-universal-identifiers.constant';

const ConvertLead = () => {
  const recordId = useRecordId();

  const execute = async () => {
    if (recordId === null) {
      throw new Error('Open one lead to convert it.');
    }

    await new RestApiClient().post(`/s${CONVERT_LEAD_ROUTE_PATH}`, {
      leadId: recordId,
    });
  };

  return <Command execute={execute} />;
};

export default defineFrontComponent({
  universalIdentifier: CONVERT_LEAD_FRONT_COMPONENT_UNIVERSAL_IDENTIFIER,
  name: 'convert-lead',
  description: 'Converts the open lead to an opportunity.',
  isHeadless: true,
  component: ConvertLead,
});
