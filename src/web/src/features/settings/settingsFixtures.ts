/** Rows the Settings tests share, shaped as the server sends them. */
import type { PermissionInfo, PersonRow, RoleRow } from "@/api/types.gen";

export const PERMISSIONS: PermissionInfo[] = [
  { key: "chat.agent.admin", group: "Chat agent", label: "Admin agent", agent: true },
  { key: "chat.agent.manager", group: "Chat agent", label: "Manager agent", agent: true },
  { key: "chat.agent.staff", group: "Chat agent", label: "Staff agent", agent: true },
  { key: "chat.agent.dealer", group: "Chat agent", label: "Dealer agent", agent: true },
  { key: "chat.agent.guest", group: "Chat agent", label: "Guest agent", agent: true },
  { key: "lookup.units", group: "Lookup", label: "Look up a unit by serial", agent: false },
  { key: "lookup.orders", group: "Lookup", label: "Look up a work order", agent: false },
  { key: "settings.people", group: "Settings", label: "Manage people", agent: false },
  { key: "settings.roles", group: "Settings", label: "Manage roles", agent: false },
];

export function person(over: Partial<PersonRow> = {}): PersonRow {
  return {
    id: "11111111-1111-1111-1111-111111111111",
    name: "Dana Otto",
    email: "dana@example.com",
    roles: [],
    agent: null,
    banned: false,
    desk: "none",
    crm: "none",
    ...over,
  };
}

export function role(over: Partial<RoleRow> = {}): RoleRow {
  return {
    id: "r-1",
    name: "Technician",
    description: null,
    builtIn: false,
    permissions: ["chat.agent.staff", "lookup.units"],
    members: 0,
    ...over,
  };
}
