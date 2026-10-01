import { RestApiClient, RestApiClientError } from 'twenty-client-sdk/rest';
import { defineFrontComponent } from 'twenty-sdk/define';
import {
  AppPath,
  Command,
  enqueueSnackbar,
  navigate,
  useRecordId,
} from 'twenty-sdk/front-component';

import {
  CONVERT_LEAD_FRONT_COMPONENT_UNIVERSAL_IDENTIFIER,
  CONVERT_LEAD_ROUTE_PATH,
} from 'src/sales/convert-lead/convert-lead-universal-identifiers.constant';

const describeConvertError = (error: unknown) => {
  if (error instanceof RestApiClientError) {
    const body = error.body as { error?: unknown } | undefined;

    if (typeof body?.error === 'string') {
      return body.error;
    }
  }

  return 'Could not convert the lead.';
};

const ConvertLead = () => {
  const recordId = useRecordId();

  // Command closes this component only when execute resolves, so a thrown
  // error would leave the button dead until the page reloads.
  const execute = async () => {
    if (recordId === null) {
      await enqueueSnackbar({
        message: 'Open one lead to convert it.',
        variant: 'error',
      });
      return;
    }

    try {
      const { opportunityId } = await new RestApiClient().post<{
        opportunityId: string;
      }>(`/s${CONVERT_LEAD_ROUTE_PATH}`, { leadId: recordId });

      await enqueueSnackbar({
        message: 'Lead converted to an opportunity.',
        variant: 'success',
      });
      await navigate(AppPath.RecordShowPage, {
        objectNameSingular: 'opportunity',
        objectRecordId: opportunityId,
      });
    } catch (error) {
      await enqueueSnackbar({
        message: describeConvertError(error),
        variant: 'error',
      });
    }
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
