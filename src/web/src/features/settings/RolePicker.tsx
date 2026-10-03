import { useId, useMemo, useState } from "react";

import type { PermissionInfo, RoleRow } from "@/api/types.gen";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

import { agentFrom } from "./agentFrom";

export function RolePicker({
  roles,
  permissions,
  value,
  onChange,
}: {
  roles: RoleRow[];
  permissions: PermissionInfo[];
  /** Picked role ids. */
  value: string[];
  onChange(next: string[]): void;
}) {
  const [find, setFind] = useState("");
  const id = useId();
  const picked = new Set(value);

  const sorted = useMemo(() => {
    const rank = (role: RoleRow) => {
      const agent = agentFrom(role.permissions, permissions);
      return agent ? permissions.indexOf(agent) : permissions.length;
    };
    return [...roles].sort((a, b) => rank(a) - rank(b) || a.name.localeCompare(b.name));
  }, [roles, permissions]);

  const shown = sorted.filter((role) =>
    role.name.toLowerCase().includes(find.trim().toLowerCase()),
  );
  const agent = agentFrom(
    roles.filter((role) => picked.has(role.id)).flatMap((role) => role.permissions),
    permissions,
  );

  function toggle(roleId: string, on: boolean) {
    onChange(on ? [...value, roleId] : value.filter((picked) => picked !== roleId));
  }

  function list(title: string, rows: RoleRow[]) {
    if (rows.length === 0) return null;
    return (
      <div className="flex flex-col gap-1">
        <h4 className="text-muted-foreground mt-3 text-xs font-medium tracking-wide uppercase">
          {title}
        </h4>
        {rows.map((role) => {
          const gives = agentFrom(role.permissions, permissions);
          return (
            <div
              key={role.id}
              className="hover:bg-accent flex items-center gap-2 rounded-sm px-1 py-1"
            >
              <Checkbox
                id={`${id}-${role.id}`}
                checked={picked.has(role.id)}
                onCheckedChange={(on) => toggle(role.id, on === true)}
              />
              <Label
                htmlFor={`${id}-${role.id}`}
                className="block min-w-0 flex-1 truncate font-normal"
                title={role.name}
              >
                {role.name}
              </Label>
              {gives ? (
                <Badge variant="secondary" className="shrink-0">
                  {gives.label}
                </Badge>
              ) : null}
            </div>
          );
        })}
      </div>
    );
  }

  return (
    <div className="flex min-w-0 flex-col gap-2">
      <Input
        placeholder="Find a role"
        value={find}
        onChange={(event) => setFind(event.target.value)}
      />
      <p className="bg-muted rounded-md px-3 py-2 text-sm">
        {agent ? (
          <>
            Chat opens the <b>{agent.label}</b>. {value.length} role{value.length === 1 ? "" : "s"}{" "}
            picked.
          </>
        ) : (
          <>
            <span className="text-amber-600 dark:text-amber-400">No agent.</span> This person can
            sign in, but chat refuses them until a role gives an agent.
          </>
        )}
      </p>
      {list(
        "Roles that give an agent",
        shown.filter((role) => agentFrom(role.permissions, permissions)),
      )}
      {list(
        "Other roles (no agent)",
        shown.filter((role) => !agentFrom(role.permissions, permissions)),
      )}
    </div>
  );
}
