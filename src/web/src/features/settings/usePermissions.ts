import { useQuery } from "@tanstack/react-query";

import { listPermissions } from "@/api/sdk.gen";

import { PERMISSIONS_QUERY_KEY } from "./permissionsQueryKey";

export function usePermissions() {
  return useQuery({
    queryKey: PERMISSIONS_QUERY_KEY,
    queryFn: async () => (await listPermissions({ throwOnError: true })).data,
  });
}
