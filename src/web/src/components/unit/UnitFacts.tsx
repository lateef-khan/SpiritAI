import type { OrderDocument, UnitHeader, WarrantyTerm } from "@/api/types.gen";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

import { asDay, asSerial, coverOf, type Cover } from "./format";

/**
 * The facts that stay pinned above the tabs.
 *
 * Pinned because the serial and the cover state are what staff check most often, and because a
 * 142-line parts list must never be able to bury them. The cover state is here rather than in the
 * fourth tab for the same reason: "is this covered" is the question the panel exists to answer, and
 * an answer behind a tab click is not pinned at all.
 */
export function UnitFacts({
  header,
  warranty,
}: {
  header: UnitHeader;
  warranty: readonly WarrantyTerm[] | null | undefined;
}) {
  const cover = coverOf(warranty);

  return (
    <div className="border-b px-3 py-2.5">
      <Title
        name={header.modelName ?? header.modelNo}
        kind={[header.isSole ? "Sole" : null, header.category].filter(Boolean).join(" · ")}
      />

      <p className="mt-1 flex items-baseline gap-2">
        <span className="text-xs text-muted-foreground">Serial</span>
        <span className="truncate font-mono text-[13px] font-medium tracking-wide tabular-nums">
          {asSerial(header.serial)}
        </span>
      </p>

      <Facts>
        <Fact label="Model">{header.modelNo}</Fact>
        <Fact label="Bought">{asDay(header.purchasedOn)}</Fact>
        <Fact label="Set up">{asDay(header.setUpOn)}</Fact>
        <Fact label="Built">{header.manufacturedOn ?? "—"}</Fact>
      </Facts>

      {cover ? <CoverLine cover={cover} /> : null}
    </div>
  );
}

/** The same shell for a work order, which has no tabs under it. */
export function OrderFacts({ order }: { order: OrderDocument }) {
  return (
    <div className="border-b px-3 py-2.5">
      <Title name={order.orderNumber} kind={order.orderType ?? "Work order"} isMono />

      <Facts>
        <Fact label="Raised">{asDay(order.orderedOn)}</Fact>
        <Fact label="Booked">{asDay(order.appointedOn)}</Fact>
        <Fact label="Shipped">{asDay(order.shippedOn)}</Fact>
        <Fact label="Closed">{asDay(order.closedOn)}</Fact>
        <Fact label="Tech">{order.technician ?? "—"}</Fact>
        <Fact label="ISP">{order.ispName ?? "—"}</Fact>
        <Fact label="Tracking">
          <span className="font-mono tabular-nums">{order.trackingNo ?? "—"}</span>
        </Fact>
      </Facts>

      <CoverLine cover={stateOf(order)} />

      {order.notes ? <p className="mt-2 text-xs text-muted-foreground">{order.notes}</p> : null}
    </div>
  );
}

/** The pinned header while it is still being read. */
export function FactsSkeleton() {
  return (
    <div className="space-y-2 border-b px-3 py-2.5">
      <Skeleton className="h-4 w-24" />
      <Skeleton className="h-3.5 w-44" />
      <Skeleton className="h-3 w-52" />
      <Skeleton className="h-3 w-36" />
    </div>
  );
}

function Title({ name, kind, isMono }: { name: string; kind: string; isMono?: boolean }) {
  return (
    <div className="flex items-baseline justify-between gap-2">
      <h2 className={cn("truncate text-sm font-semibold", isMono && "font-mono tabular-nums")}>
        {name}
      </h2>
      <span className="shrink-0 text-xs text-muted-foreground">{kind}</span>
    </div>
  );
}

/**
 * The small facts, in a row that wraps.
 *
 * The panel is resizable, so a fixed two-column grid either wastes half the width or clips at the
 * narrow end. Wrapping keeps every fact readable at any width the person drags it to.
 */
function Facts({ children }: { children: React.ReactNode }) {
  return <div className="mt-1.5 flex flex-wrap gap-x-4 gap-y-1 text-xs">{children}</div>;
}

function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <span className="text-muted-foreground">
      {label} <span className="font-medium text-foreground">{children}</span>
    </span>
  );
}

/** One line, one dot, one answer. */
function CoverLine({ cover }: { cover: Cover }) {
  return (
    <p className="mt-2 flex items-center gap-2 text-xs">
      <span
        aria-hidden
        className={cn(
          "size-1.5 shrink-0 rounded-full",
          cover.state === "in" && "bg-aui-success",
          cover.state === "out" && "bg-destructive",
          cover.state === "unknown" && "bg-muted-foreground",
        )}
      />
      <span className="font-medium">{cover.label}</span>
      <span className="truncate text-muted-foreground">{cover.detail}</span>
    </p>
  );
}

/** A work order's own state, written the same way the cover state is. */
function stateOf(order: OrderDocument): Cover {
  if (order.closedOn) return { state: "out", label: "Closed", detail: asDay(order.closedOn) };

  if (order.shippedOn) {
    return { state: "in", label: "Shipped", detail: `Not closed, sent ${asDay(order.shippedOn)}` };
  }

  return { state: "unknown", label: "Open", detail: "Not shipped, not closed" };
}
