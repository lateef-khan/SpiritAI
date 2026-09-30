/**
 * The Settings page: a section sidebar and, for now, the one section it opens on — People.
 *
 * `SECTIONS` is a small array on purpose: a later section is one more entry here, not a new
 * layout. Admin-only server-side; a 403 from the list means somebody opened the page directly
 * without the role, and gets a message instead of an empty table.
 */
import { useQuery, type UseQueryResult } from "@tanstack/react-query";
import { Users } from "lucide-react";

import { listPeople } from "@/api/sdk.gen";
import type { AccessGroup, PersonRow } from "@/api/types.gen";
import { Badge } from "@/components/ui/badge";
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarHeader,
  SidebarInset,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarTrigger,
} from "@/components/ui/sidebar";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { HostRefusedError } from "@/lib/apiClient";

import { PersonLinkButton } from "./PersonLinkButton";
import { PEOPLE_QUERY_KEY } from "./peopleQueryKey";

const SECTIONS = [{ id: "people", label: "People", icon: Users }] as const;

/** "TechServiceManager" → "Tech Service Manager": every group name is PascalCase, so a lower-to-upper edge is a word break. */
function groupWords(group: AccessGroup): string {
  return group.replace(/([a-z0-9])([A-Z])/g, "$1 $2");
}

export function SettingsPage() {
  const people = useQuery({
    queryKey: PEOPLE_QUERY_KEY,
    queryFn: async () => (await listPeople({ throwOnError: true })).data,
  });

  return (
    <SidebarProvider>
      <Sidebar>
        <SidebarHeader>
          <span className="px-2 py-1.5 text-sm font-semibold">Settings</span>
        </SidebarHeader>
        <SidebarContent>
          <SidebarGroup>
            <SidebarMenu>
              {SECTIONS.map((section) => (
                <SidebarMenuItem key={section.id}>
                  <SidebarMenuButton isActive={section.id === "people"}>
                    <section.icon />
                    <span>{section.label}</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroup>
        </SidebarContent>
      </Sidebar>
      <SidebarInset>
        <header className="flex h-12 flex-none items-center gap-2 border-b px-2.5">
          <SidebarTrigger />
          <h1 className="font-semibold">People</h1>
        </header>
        <main className="min-h-0 flex-1 overflow-auto p-4">
          <PeopleSection people={people} />
        </main>
      </SidebarInset>
    </SidebarProvider>
  );
}

function PeopleSection({ people }: { people: UseQueryResult<PersonRow[]> }) {
  if (people.error instanceof HostRefusedError && people.error.status === 403) {
    return <p>Only admins can open Settings.</p>;
  }

  if (people.isPending) {
    return <p className="text-muted-foreground">Loading…</p>;
  }

  if (people.error || !people.data) {
    return <p className="text-destructive">Could not load People.</p>;
  }

  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Name</TableHead>
          <TableHead>Email</TableHead>
          <TableHead>Groups</TableHead>
          <TableHead>Desk</TableHead>
          <TableHead>CRM</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {people.data.map((person) => (
          <TableRow key={person.id}>
            <TableCell>{person.name}</TableCell>
            <TableCell>{person.email}</TableCell>
            <TableCell>
              <div className="flex flex-wrap gap-1">
                {person.groups.map((group) => (
                  <Badge key={group} variant="secondary">
                    {groupWords(group)}
                  </Badge>
                ))}
              </div>
            </TableCell>
            <TableCell>
              <PersonLinkButton personId={person.id} app="desk" state={person.desk} />
            </TableCell>
            <TableCell>
              <PersonLinkButton personId={person.id} app="crm" state={person.crm} />
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
