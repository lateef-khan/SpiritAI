import { Badge } from "@/components/ui/badge";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

import type { InboxCounts, InboxTab, InboxView } from "../hooks/useHandoffs";

/**
 * The Mine / Unassigned / All split of whichever view is open.
 *
 * The done view has no unassigned rows — a closed handoff has already been claimed — so that tab
 * is not drawn at all rather than drawn disabled. `tab` here is already the effective tab
 * `InboxPanel` resolved, so this component never has to know about the unassigned-in-done
 * fallback.
 */
export function InboxTabs({
  view,
  tab,
  onTabChange,
  counts,
}: {
  view: InboxView;
  tab: InboxTab;
  onTabChange: (tab: InboxTab) => void;
  counts: InboxCounts;
}) {
  return (
    <Tabs
      value={tab}
      onValueChange={(value) => onTabChange(value as InboxTab)}
      className="shrink-0 gap-0 px-2 pt-2"
    >
      <TabsList className="h-auto w-full justify-start gap-3 rounded-none border-b bg-transparent p-0">
        <Tab value="mine" label="Mine" count={counts.mine} />
        {view === "open" ? (
          <Tab value="unassigned" label="Unassigned" count={counts.unassigned} />
        ) : null}
        <Tab value="all" label="All" count={counts.all} />
      </TabsList>
    </Tabs>
  );
}

function Tab({ value, label, count }: { value: InboxTab; label: string; count: number }) {
  return (
    <TabsTrigger
      value={value}
      className="flex-none items-center gap-1.5 rounded-none border-b-2 border-transparent px-1 pb-2.5 text-sm font-normal text-muted-foreground shadow-none data-[state=active]:border-b-primary data-[state=active]:bg-transparent data-[state=active]:text-foreground data-[state=active]:shadow-none"
    >
      {label}
      <Badge variant="secondary" className="rounded-md px-1.5 tabular-nums">
        {count}
      </Badge>
    </TabsTrigger>
  );
}
