import { useIdentifiers, type Identifier, type Said } from "@/hooks/useIdentifiers";
import { cn } from "@/lib/utils";

import { FactsSkeleton, OrderFacts, UnitFacts } from "./UnitFacts";
import { TabsSkeleton, UnitTabs } from "./UnitTabs";
import { useUnitDocument } from "./useUnitDocument";

/**
 * A pinned, always-visible view of the one machine the conversation is about.
 *
 * Deterministic, and no model is involved. `draw` already builds a model-shaped card per answer and
 * that is the flexible half; this is the other half — the same few facts, always in the same place,
 * never re-shaped, never scrolling away.
 *
 * It learns which unit to show by watching the person's own turns for a number. The agent does not
 * know this exists.
 */
export function UnitPanel({
  said,
  onAsk,
  isRunning = false,
  className,
}: {
  said: readonly Said[];
  onAsk: (question: string) => void;
  isRunning?: boolean;
  className?: string;
}) {
  const { chips, selected, select } = useIdentifiers(said);
  const { view, retry } = useUnitDocument(selected);

  // A serial names a machine and an order names a job. The two documents share no fields, so
  // selecting an order does not filter the machine — it replaces the whole panel. One flat row of
  // pills hides that; a labelled bar per kind cannot.
  const serials = chips.filter((chip) => chip.kind === "serial");
  const orders = chips.filter((chip) => chip.kind === "order");

  return (
    <aside
      aria-label="Unit"
      data-testid="unit-panel"
      className={cn("flex h-full min-h-0 w-full flex-col border-l bg-card", className)}
    >
      {serials.length > 0 || orders.length > 0 ? (
        <div className="shrink-0 border-b py-1">
          {serials.length > 0 ? (
            <IdBar label="Serial" ids={serials} selected={selected} onSelect={select} />
          ) : null}
          {orders.length > 0 ? (
            <IdBar label="Work order" ids={orders} selected={selected} onSelect={select} />
          ) : null}
        </div>
      ) : null}

      <div className="flex min-h-0 flex-1 flex-col">
        {view.state === "idle" ? (
          <Empty title="No information yet" />
        ) : view.state === "loading" ? (
          <>
            <FactsSkeleton />
            <TabsSkeleton />
          </>
        ) : view.state === "missing" ? (
          <Empty
            title="No unit with that number"
            detail={`Nothing on file for ${view.identifier.value}.`}
          />
        ) : view.state === "failed" ? (
          <Empty title="Could not reach the database" detail="Nothing was read." onRetry={retry} />
        ) : view.state === "order" ? (
          <OrderLines view={view} />
        ) : view.unit.header ? (
          <>
            <UnitFacts header={view.unit.header} warranty={view.unit.warranty} />
            <UnitTabs unit={view.unit} onAsk={onAsk} disabled={isRunning} />
          </>
        ) : (
          <Empty
            title="Could not read this unit"
            detail="The lookup answered nothing."
            onRetry={retry}
          />
        )}
      </div>
    </aside>
  );
}

/**
 * Every number of one kind this thread has mentioned, newest first.
 *
 * One small feature covers three problems: a correction that was itself wrong, two numbers in one
 * message, and flipping between two machines. Chasing a wrong one costs a single cheap request.
 *
 * Both bars stay drawn whatever is selected, so a person reading a work order still has the machine
 * they came from one click away — which no lookup could give them, because an order number names no
 * serial anywhere in the database.
 *
 * The row scrolls sideways rather than wrapping or shortening. A serial is read digit by digit
 * against a sticker on a frame, so a number this bar cut short would be a number nobody could
 * check, and a bar that grew a line per handful of numbers would push the machine off the screen.
 */
function IdBar({
  ids,
  selected,
  onSelect,
  label,
}: {
  ids: readonly Identifier[];
  selected: Identifier | null;
  onSelect: (value: string) => void;
  label: string;
}) {
  return (
    <div role="group" aria-label={label} className="flex items-center gap-2 px-3.5 py-1">
      <span className="w-16 shrink-0 text-[11px] text-muted-foreground">{label}</span>

      <div className="flex min-w-0 flex-1 gap-1.5 overflow-x-auto py-0.5 [scrollbar-width:thin]">
        {ids.map((id) => (
          <button
            key={id.value}
            type="button"
            aria-pressed={id.value === selected?.value}
            onClick={() => onSelect(id.value)}
            className={cn(
              "shrink-0 rounded-full border px-2.5 py-0.5 font-mono text-xs whitespace-nowrap tabular-nums",
              id.value === selected?.value
                ? "border-transparent bg-foreground text-background"
                : "bg-background text-muted-foreground hover:border-muted-foreground hover:text-foreground",
            )}
          >
            {id.value}
          </button>
        ))}
      </div>
    </div>
  );
}

function OrderLines({
  view,
}: {
  view: Extract<ReturnType<typeof useUnitDocument>["view"], { state: "order" }>;
}) {
  return (
    <>
      <OrderFacts order={view.order} />
      <div className="min-h-0 flex-1 overflow-y-auto">
        {view.order.lines.length === 0 ? (
          <p className="p-3.5 text-xs text-muted-foreground">No parts on this order.</p>
        ) : (
          <ul className="divide-y">
            {view.order.lines.map((line, index) => (
              <li
                key={`${line.partNo}-${index}`}
                className="flex items-baseline gap-2 px-3.5 py-2 text-xs"
              >
                <span className="w-24 shrink-0 font-mono tabular-nums">{line.partNo}</span>
                <span className="flex-1 truncate">{line.description ?? "—"}</span>
                <span className="tabular-nums text-muted-foreground">×{line.shipped ?? 0}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
    </>
  );
}

function Empty({
  title,
  detail,
  onRetry,
}: {
  title: string;
  detail?: string;
  onRetry?: () => void;
}) {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-1 p-6 text-center">
      <p className="text-sm font-medium">{title}</p>
      {detail ? <p className="text-xs text-muted-foreground">{detail}</p> : null}
      {onRetry ? (
        <button type="button" onClick={onRetry} className="mt-2 text-xs underline">
          Try again
        </button>
      ) : null}
    </div>
  );
}
