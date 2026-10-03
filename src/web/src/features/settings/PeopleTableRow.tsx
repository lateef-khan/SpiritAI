import type { ComponentProps } from "react";

import type { PermissionInfo, PersonRow } from "@/api/types.gen";
import { Badge } from "@/components/ui/badge";
import { TableCell, TableRow } from "@/components/ui/table";

import { PersonActions } from "./PersonActions";
import { PersonLinkButton } from "./PersonLinkButton";

const AMBER = "bg-amber-100 text-amber-800 dark:bg-amber-950 dark:text-amber-300";

function StatusBadge({ person }: { person: PersonRow }) {
  if (person.banned) return <Badge variant="destructive">Banned</Badge>;
  if (person.roles.length === 0) {
    return (
      <Badge variant="secondary" className={AMBER}>
        No roles
      </Badge>
    );
  }
  if (person.agent === null) {
    return (
      <Badge variant="secondary" className={AMBER}>
        No agent
      </Badge>
    );
  }
  return null;
}

export function PeopleTableRow({
  person,
  catalog,
  isYou,
  rowProps,
}: {
  person: PersonRow;
  /** The server's permission list, for the agent's label. */
  catalog: PermissionInfo[];
  isYou: boolean;
  rowProps?: ComponentProps<typeof TableRow> & { "data-index"?: number };
}) {
  const dim = person.banned ? "opacity-60" : undefined;
  return (
    <TableRow data-banned={person.banned} {...rowProps}>
      <TableCell className={dim}>
        <div className="flex min-w-0 items-center gap-2 font-medium">
          <span className="truncate" title={person.name}>
            {person.name}
          </span>
          {isYou ? <Badge variant="outline">You</Badge> : null}
        </div>
        <div className="text-muted-foreground truncate text-xs" title={person.email}>
          {person.email}
        </div>
      </TableCell>
      <TableCell className={dim}>
        <div className="flex flex-wrap items-center gap-1">
          <StatusBadge person={person} />
          {person.agent ? (
            <Badge variant="secondary">
              {catalog.find((info) => info.key === person.agent)?.label ?? person.agent}
            </Badge>
          ) : null}
        </div>
        {person.roles.length > 0 ? (
          <div
            className="text-muted-foreground mt-1 truncate text-xs"
            title={person.roles.map((role) => role.name).join(", ")}
          >
            Roles: {person.roles.map((role) => role.name).join(", ")}
          </div>
        ) : null}
      </TableCell>
      <TableCell className={dim}>
        <PersonLinkButton
          person={person}
          app="desk"
          state={person.desk}
          disabled={person.banned}
          isYou={isYou}
        />
      </TableCell>
      <TableCell className={dim}>
        <PersonLinkButton
          person={person}
          app="crm"
          state={person.crm}
          disabled={person.banned}
          isYou={isYou}
        />
      </TableCell>
      <TableCell>
        <PersonActions person={person} isYou={isYou} />
      </TableCell>
    </TableRow>
  );
}
