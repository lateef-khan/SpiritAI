/**
 * Who the signed-in person is and what they may do, from `GET /v1/me`. Pages draw only what this
 * allows; the server still checks every call.
 */
import { useQuery } from "@tanstack/react-query";

import { getMe } from "@/api/sdk.gen";
import type { Permission } from "@/api/types.gen";

export const ME_QUERY_KEY = ["me"] as const;

export function useMe() {
  return useQuery({
    queryKey: ME_QUERY_KEY,
    queryFn: async () => (await getMe({ throwOnError: true })).data,
  });
}

/** Whether the person holds `permission`. False until the server has answered. */
export function useCan(permission: Permission): boolean {
  return useMe().data?.permissions.includes(permission) ?? false;
}
