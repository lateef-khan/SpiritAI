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

  return (
    <aside
      aria-label="Unit"
      data-testid="unit-panel"
      className={cn("flex h-full min-h-0 w-full flex-col border-l bg-card", className)}
    >
      {chips.length > 0 ? <Chips chips={chips} selected={selected} onSelect={select} /> : null}

      {view.state === "idle" ? (
        <Empty
          title="No unit yet"
          detail="Paste a serial number or a work order number and it will show up here."
        />
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
          <UnitFacts header={view.unit.header} />
          <UnitTabs unit={view.unit} onAsk={onAsk} disabled={isRunning} />
        </>
      ) : (
        <Empty
          title="Could not read this unit"
          detail="The lookup answered nothing."
          onRetry={retry}
        />
      )}
    </aside>
  );
}

/**
 * Every number this thread has mentioned, newest first and selected.
 *
 * One small feature covers three problems: a correction that was itself wrong, two numbers in one
 * message, and flipping between two machines. Chasing a wrong one costs a single cheap request.
 */
function Chips({
  chips,
  selected,
  onSelect,
}: {
  chips: readonly Identifier[];
  selected: Identifier | null;
  onSelect: (value: string) => void;
}) {
  return (
    <div className="flex flex-wrap gap-1 border-b p-2">
      {chips.map((chip) => (
        <button
          key={chip.value}
          type="button"
          aria-pressed={chip.value === selected?.value}
          onClick={() => onSelect(chip.value)}
          className={cn(
            "rounded-full border px-2 py-0.5 font-mono text-xs tabular-nums",
            chip.value === selected?.value
              ? "border-transparent bg-primary text-primary-foreground"
              : "text-muted-foreground hover:bg-accent",
          )}
        >
          {chip.kind === "serial" ? `…${chip.value.slice(-8)}` : chip.value}
        </button>
      ))}
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
          <p className="p-3 text-xs text-muted-foreground">No parts on this order.</p>
        ) : (
          <ul className="divide-y">
            {view.order.lines.map((line, index) => (
              <li
                key={`${line.partNo}-${index}`}
                className="flex items-baseline gap-2 px-3 py-2 text-xs"
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
  detail: string;
  onRetry?: () => void;
}) {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-1 p-6 text-center">
      <p className="text-sm font-medium">{title}</p>
      <p className="text-xs text-muted-foreground">{detail}</p>
      {onRetry ? (
        <button type="button" onClick={onRetry} className="mt-2 text-xs underline">
          Try again
        </button>
      ) : null}
    </div>
  );
}
