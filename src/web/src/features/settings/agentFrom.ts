import type { Permission, PermissionInfo } from "@/api/types.gen";

/**
 * The chat agent `held` gives. The order comes from the server's permission list, which lists the
 * agents strongest first, so the web app keeps no copy of it.
 */
export function agentFrom(
  held: readonly Permission[],
  catalog: readonly PermissionInfo[],
): PermissionInfo | null {
  return catalog.find((info) => info.agent && held.includes(info.key)) ?? null;
}
