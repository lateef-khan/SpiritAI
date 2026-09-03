import type { UnitDocument, UnitJob, UnitPart, UnitSection, WarrantyTerm } from "@/api/types.gen";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { cn } from "@/lib/utils";

import { asDay } from "./format";

/**
 * The scrolling half of the panel.
 *
 * Tabs rather than one long column, for the same reason the header is pinned: a 142-line parts list
 * must not be able to bury the six facts a person actually came for.
 */
export function UnitTabs({
  unit,
  onAsk,
  disabled,
}: {
  unit: UnitDocument;
  onAsk: (question: string) => void;
  disabled: boolean;
}) {
  const missing = new Set<UnitSection>(unit.unavailable);

  return (
    <Tabs defaultValue="jobs" className="min-h-0 flex-1 gap-0">
      <TabsList className="h-auto shrink-0 justify-start gap-0 overflow-x-auto rounded-none border-b bg-transparent p-0 px-2 [scrollbar-width:none]">
        <Tab value="jobs" label="Jobs" count={unit.jobs?.length} />
        <Tab value="history" label="History" count={unit.history?.length} />
        <Tab value="parts" label="Parts" count={unit.parts?.length} />
        <Tab value="warranty" label="Warranty" count={unit.warranty?.length} />
      </TabsList>

      <Panel value="jobs" missing={missing.has("jobs")} rows={unit.jobs} empty="No open jobs.">
        {(jobs) =>
          jobs.map((job) => <JobRow key={job.orderNumber} {...{ job, onAsk, disabled }} />)
        }
      </Panel>

      <Panel
        value="history"
        missing={missing.has("history")}
        rows={unit.history}
        empty="No service calls."
      >
        {(rows) =>
          rows.map((job) => <JobRow key={job.orderNumber} {...{ job, onAsk, disabled }} />)
        }
      </Panel>

      <Panel value="parts" missing={missing.has("parts")} rows={unit.parts} empty="No parts list.">
        {(parts) =>
          parts.map((part, index) => (
            <PartRow key={`${part.spNo}-${index}`} {...{ part, onAsk, disabled }} />
          ))
        }
      </Panel>

      <Panel
        value="warranty"
        missing={missing.has("warranty")}
        rows={unit.warranty}
        empty="No warranty on file."
      >
        {(terms) => terms.map((term) => <WarrantyRow key={term.category} term={term} />)}
      </Panel>
    </Tabs>
  );
}

/** The panel while it is still being read. */
export function TabsSkeleton() {
  return (
    <div className="space-y-2 p-3.5">
      {[0, 1, 2, 3, 4].map((row) => (
        <Skeleton key={row} className="h-8 w-full" />
      ))}
    </div>
  );
}

/** One tab, underlined when it is the open one — the count is the answer to "is anything here?". */
function Tab({ value, label, count }: { value: string; label: string; count?: number }) {
  return (
    <TabsTrigger
      value={value}
      className={cn(
        "flex-none items-baseline gap-1 rounded-none border-b-2 border-transparent px-2.5 py-2 text-[13px] font-normal text-muted-foreground shadow-none hover:text-foreground",
        "data-[state=active]:border-b-foreground data-[state=active]:bg-transparent data-[state=active]:font-medium data-[state=active]:text-foreground data-[state=active]:shadow-none",
      )}
    >
      {label}
      {count === undefined ? null : (
        <span className="text-[11px] tabular-nums text-muted-foreground">{count}</span>
      )}
    </TabsTrigger>
  );
}

/**
 * One tab's contents, or the reason it has none.
 *
 * A section the host could not read says so where its rows would be. The rest of the panel still
 * renders, because one tool failing is not the machine going away.
 */
function Panel<T>({
  value,
  missing,
  rows,
  empty,
  children,
}: {
  value: string;
  missing: boolean;
  rows: readonly T[] | null | undefined;
  empty: string;
  children: (rows: readonly T[]) => React.ReactNode;
}) {
  return (
    <TabsContent value={value} className="min-h-0 overflow-y-auto">
      {missing || !rows ? (
        <p className="p-3.5 text-xs text-muted-foreground">Could not load this.</p>
      ) : rows.length === 0 ? (
        <p className="p-3.5 text-xs text-muted-foreground">{empty}</p>
      ) : (
        <ul className="divide-y">{children(rows)}</ul>
      )}
    </TabsContent>
  );
}

function JobRow({
  job,
  onAsk,
  disabled,
}: {
  job: UnitJob;
  onAsk: (question: string) => void;
  disabled: boolean;
}) {
  const isClosed = job.status === "closed";

  return (
    <li>
      <button
        type="button"
        inert={disabled}
        onClick={() => onAsk(`Tell me about work order ${job.orderNumber}.`)}
        className="flex w-full gap-2.5 px-3.5 py-2.5 text-left hover:bg-accent"
      >
        <span className="min-w-0 flex-1">
          <span className="block font-mono text-[13px] font-medium tabular-nums">
            {job.orderNumber}
          </span>

          {/* On its own line, and never truncated: what went wrong is the reason to read the row. */}
          <span className="mt-0.5 block text-xs text-muted-foreground">
            {[job.summary ?? "—", job.technician].filter(Boolean).join(" · ")}
          </span>
        </span>

        <span className="shrink-0 text-right">
          <span
            className={cn(
              "inline-block rounded border px-1.5 py-px text-[11px]",
              isClosed
                ? "border-aui-success/40 text-aui-success"
                : "border-chart-2/45 text-chart-2",
            )}
          >
            {job.statusText ?? job.status}
          </span>

          <span className="mt-1 block text-[11px] whitespace-nowrap text-muted-foreground">
            {asDay(job.calledOn)}
          </span>
        </span>
      </button>
    </li>
  );
}

function PartRow({
  part,
  onAsk,
  disabled,
}: {
  part: UnitPart;
  onAsk: (question: string) => void;
  disabled: boolean;
}) {
  return (
    <Row
      disabled={disabled}
      onClick={() => onAsk(`Tell me about part ${part.spNo}.`)}
      lead={<span className="font-mono tabular-nums">{part.spNo}</span>}
      body={part.description ?? "—"}
      trail={<span className="tabular-nums text-muted-foreground">×{part.quantity ?? 1}</span>}
    />
  );
}

/** Warranty rows are not clickable: there is nothing further to ask about a date. */
function WarrantyRow({ term }: { term: WarrantyTerm }) {
  return (
    <li className="flex items-baseline gap-2 px-3.5 py-2 text-xs">
      <span className="w-24 shrink-0 font-medium">{term.category}</span>
      <span className="flex-1 text-muted-foreground">{asDay(term.expiresOn)}</span>
      <span className={term.isCovered ? "font-medium" : "text-muted-foreground"}>
        {term.isCovered === null ? "No date" : term.isCovered ? "Covered" : "Expired"}
      </span>
    </li>
  );
}

/**
 * One clickable row.
 *
 * A click appends a question through the same path a drawn button uses, and is suppressed while a
 * turn is running for the same reason: appending mid-stream aborts the turn rather than queueing
 * behind it. `inert` stops the person believing the click landed.
 */
function Row({
  onClick,
  disabled,
  lead,
  body,
  trail,
}: {
  onClick: () => void;
  disabled: boolean;
  lead: React.ReactNode;
  body: string;
  trail: React.ReactNode;
}) {
  return (
    <li>
      <button
        type="button"
        inert={disabled}
        onClick={onClick}
        className="flex w-full items-baseline gap-2 px-3.5 py-2 text-left text-xs hover:bg-accent disabled:opacity-60"
      >
        <span className="w-24 shrink-0">{lead}</span>
        <span className="flex-1 truncate">{body}</span>
        <span className="flex shrink-0 flex-col items-end gap-0.5">{trail}</span>
      </button>
    </li>
  );
}
