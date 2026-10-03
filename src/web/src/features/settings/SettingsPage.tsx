import { type LucideIcon, ShieldCheck, Users } from "lucide-react";
import { useState } from "react";

import type { Permission } from "@/api/types.gen";
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
import { useMe } from "@/features/auth/useMe";

import { PeopleSection } from "./PeopleSection";
import { RolesSection } from "./RolesSection";

type Section = { id: "people" | "roles"; label: string; icon: LucideIcon; permission: Permission };

const SECTIONS: Section[] = [
  { id: "people", label: "People", icon: Users, permission: "settings.people" },
  { id: "roles", label: "Roles", icon: ShieldCheck, permission: "settings.roles" },
];

export function SettingsPage() {
  const me = useMe();
  const [scrollElement, setScrollElement] = useState<HTMLElement | null>(null);
  const [chosen, setChosen] = useState<Section["id"] | null>(null);

  const held = me.data?.permissions ?? [];
  const sections = SECTIONS.filter((section) => held.includes(section.permission));
  const current = sections.find((section) => section.id === chosen) ?? sections[0] ?? null;

  return (
    <SidebarProvider className="h-svh">
      <Sidebar>
        <SidebarHeader>
          <span className="px-2 py-1.5 text-sm font-semibold">Settings</span>
        </SidebarHeader>
        <SidebarContent>
          <SidebarGroup>
            <SidebarMenu>
              {sections.map((section) => (
                <SidebarMenuItem key={section.id}>
                  <SidebarMenuButton
                    isActive={section.id === current?.id}
                    onClick={() => setChosen(section.id)}
                  >
                    <section.icon />
                    <span>{section.label}</span>
                  </SidebarMenuButton>
                </SidebarMenuItem>
              ))}
            </SidebarMenu>
          </SidebarGroup>
        </SidebarContent>
      </Sidebar>
      <SidebarInset className="min-h-0 overflow-hidden">
        <header className="flex h-12 flex-none items-center gap-2 border-b px-2.5">
          <SidebarTrigger />
          <h1 className="font-semibold">{current?.label ?? "Settings"}</h1>
        </header>
        <main ref={setScrollElement} className="min-h-0 flex-1 overflow-auto">
          {me.isPending ? (
            <p className="text-muted-foreground p-4">Loading…</p>
          ) : me.error ? (
            <p className="text-destructive p-4">Could not load Settings.</p>
          ) : current === null ? (
            <p className="p-4">Your roles do not open Settings.</p>
          ) : current.id === "people" ? (
            <PeopleSection scrollElement={scrollElement} />
          ) : (
            <RolesSection scrollElement={scrollElement} />
          )}
        </main>
      </SidebarInset>
    </SidebarProvider>
  );
}
