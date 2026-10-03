/**
 * Settings → People: the people table, searched and filtered here, drawn a screenful at a time.
 * A 403 from the list means somebody reached it without `settings.people`.
 */
import { useQuery } from "@tanstack/react-query";
import { useVirtualizer } from "@tanstack/react-virtual";
import { PlusIcon } from "lucide-react";
import { useState } from "react";

import { listPeople } from "@/api/sdk.gen";
import type { PersonRow } from "@/api/types.gen";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { useSession } from "@/features/auth/authClient";
import { HostRefusedError } from "@/lib/apiClient";

import { AddPersonDialog } from "./AddPersonDialog";
import { PeopleTableRow } from "./PeopleTableRow";
import { PEOPLE_QUERY_KEY } from "./peopleQueryKey";
import { usePermissions } from "./usePermissions";
import { SpacerRow, spacersOf } from "./VirtualSpacerRow";

const ROW_HEIGHT = 57;

type Filter = "all" | "active" | "banned";

export function PeopleSection({ scrollElement }: { scrollElement: HTMLElement | null }) {
  const people = useQuery({
    queryKey: PEOPLE_QUERY_KEY,
    queryFn: async () => (await listPeople({ throwOnError: true })).data,
  });

  if (people.error instanceof HostRefusedError && people.error.status === 403) {
    return <p className="p-4">Your roles do not open People.</p>;
  }

  if (people.isPending) {
    return <p className="text-muted-foreground p-4">Loading…</p>;
  }

  if (people.error || !people.data) {
    return <p className="text-destructive p-4">Could not load People.</p>;
  }

  return <PeopleList rows={people.data} scrollElement={scrollElement} />;
}

function PeopleList({
  rows,
  scrollElement,
}: {
  rows: PersonRow[];
  scrollElement: HTMLElement | null;
}) {
  const session = useSession();
  const permissions = usePermissions();
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<Filter>("all");
  const [adding, setAdding] = useState(false);

  const needle = search.trim().toLowerCase();
  const shown = rows.filter(
    (person) =>
      (filter === "all" || person.banned === (filter === "banned")) &&
      (needle === "" ||
        person.name.toLowerCase().includes(needle) ||
        person.email.toLowerCase().includes(needle)),
  );
  const myId = session.data?.user.id;

  const virtualizer = useVirtualizer({
    count: shown.length,
    getScrollElement: () => scrollElement,
    estimateSize: () => ROW_HEIGHT,
    getItemKey: (index) => shown[index]!.id,
    overscan: 10,
  });
  const { items, above, below } = spacersOf(virtualizer);

  return (
    <div className="flex flex-col px-4 pb-4">
      <div className="bg-background sticky top-0 z-20 flex h-14 flex-none items-center gap-2">
        <Input
          className="w-60"
          placeholder="Search name or email"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <ToggleGroup
          type="single"
          variant="outline"
          value={filter}
          onValueChange={(value) => {
            if (value) setFilter(value as Filter);
          }}
        >
          <ToggleGroupItem value="all">All</ToggleGroupItem>
          <ToggleGroupItem value="active">Active</ToggleGroupItem>
          <ToggleGroupItem value="banned">Banned</ToggleGroupItem>
        </ToggleGroup>
        <div className="flex-1" />
        <Button onClick={() => setAdding(true)}>
          <PlusIcon />
          Add person
        </Button>
      </div>
      {/* The table's own wrapper scrolls sideways, which would trap the sticky header inside it. */}
      <div className="[&_[data-slot=table-container]]:overflow-visible">
        <Table className="table-fixed">
          <TableHeader className="bg-background sticky top-14 z-10">
            <TableRow>
              <TableHead className="w-[30%]">Person</TableHead>
              <TableHead>Access</TableHead>
              <TableHead className="w-40">Desk</TableHead>
              <TableHead className="w-40">CRM</TableHead>
              <TableHead className="w-12" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {shown.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} className="text-muted-foreground text-center">
                  Nobody matches.
                </TableCell>
              </TableRow>
            ) : (
              <>
                <SpacerRow height={above} colSpan={5} />
                {items.map((item) => {
                  const person = shown[item.index]!;
                  return (
                    <PeopleTableRow
                      key={item.key}
                      person={person}
                      catalog={permissions.data ?? []}
                      isYou={person.id === myId}
                      rowProps={{ "data-index": item.index, ref: virtualizer.measureElement }}
                    />
                  );
                })}
                <SpacerRow height={below} colSpan={5} />
              </>
            )}
          </TableBody>
        </Table>
      </div>
      <AddPersonDialog open={adding} onOpenChange={setAdding} />
    </div>
  );
}
