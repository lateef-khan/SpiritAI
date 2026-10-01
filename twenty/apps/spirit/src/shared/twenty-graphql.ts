export type GraphqlCaller = 'person' | 'application';

export type GraphqlError = {
  message: string;
  extensions?: Record<string, unknown>;
};

export type GraphqlResponse<TData> = {
  data?: TData | null;
  errors?: GraphqlError[];
};

const TOKEN_ENV_NAME_BY_CALLER: Record<GraphqlCaller, string> = {
  person: 'TWENTY_APP_ACCESS_TOKEN',
  application: 'TWENTY_APP_APPLICATION_ACCESS_TOKEN',
};

export const twentyGraphql = async <TData>(
  caller: GraphqlCaller,
  query: string,
  variables?: Record<string, unknown>,
): Promise<GraphqlResponse<TData>> => {
  const response = await fetch(`${process.env.TWENTY_API_URL}/graphql`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      authorization: `Bearer ${process.env[TOKEN_ENV_NAME_BY_CALLER[caller]]}`,
    },
    body: JSON.stringify({ query, variables }),
  });

  return (await response.json()) as GraphqlResponse<TData>;
};

export const describeGraphqlErrors = (errors: GraphqlError[] | undefined) =>
  (errors ?? []).map((error) => error.message).join('; ') || 'no data';
