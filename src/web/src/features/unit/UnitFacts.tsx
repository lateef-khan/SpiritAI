import type { OrderDocument, UnitHeader, WarrantyTerm } from "@/api/types.gen";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

import { asDay, asKind, asSerial, coverOf, type Cover } from "./format";

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
    <div className="border-b px-3.5 py-3">
      <Title name={header.modelName ?? header.modelNo} />

      <p className="mt-1 text-xs text-muted-foreground">
        {[header.isSole ? "Sole" : null, asKind(header.category)].filter(Boolean).join(" · ")}
        {header.isSole || header.category ? " · " : null}
        model <span className="font-mono tabular-nums">{header.modelNo}</span>
        {header.manufacturedOn ? ` · built ${header.manufacturedOn}` : null}
      </p>

      {cover ? <CoverPill cover={cover} /> : null}

      <p className="mt-3 text-[11px] text-muted-foreground">Serial</p>
      <p className="font-mono text-[13px] font-medium tracking-wide tabular-nums">
        {asSerial(header.serial)}
      </p>

      <Facts>
        <Fact label="Bought">{asDay(header.purchasedOn)}</Fact>
        <Fact label="Set up">{asDay(header.setUpOn)}</Fact>
      </Facts>
    </div>
  );
}

/** The same shell for a work order, which has no tabs under it. */
export function OrderFacts({ order }: { order: OrderDocument }) {
  return (
    <div className="border-b px-3.5 py-3">
      <Title name={order.orderNumber} isMono />

      <p className="mt-1 text-xs text-muted-foreground">
        {[order.orderType ?? "Work order", order.ispName].filter(Boolean).join(" · ")}
      </p>

      <CoverPill cover={stateOf(order)} />

      <Facts>
        <Fact label="Raised">{asDay(order.orderedOn)}</Fact>
        <Fact label="Booked">{asDay(order.appointedOn)}</Fact>
        <Fact label="Shipped">{asDay(order.shippedOn)}</Fact>
        <Fact label="Closed">{asDay(order.closedOn)}</Fact>
        <Fact label="Tech">{order.technician ?? "—"}</Fact>
        <Fact label="Tracking">
          <span className="font-mono tabular-nums">{order.trackingNo ?? "—"}</span>
        </Fact>
      </Facts>

      {order.notes ? <p className="mt-2 text-xs text-muted-foreground">{order.notes}</p> : null}
    </div>
  );
}

/** The pinned header while it is still being read. */
export function FactsSkeleton() {
  return (
    <div className="space-y-2 border-b px-3.5 py-3">
      <Skeleton className="h-5 w-32" />
      <Skeleton className="h-3.5 w-44" />
      <Skeleton className="h-5 w-52 rounded-full" />
      <Skeleton className="h-4 w-40" />
    </div>
  );
}

/** The machine's own name, set large enough to be the first thing read. */
function Title({ name, isMono }: { name: string; isMono?: boolean }) {
  return (
    <h2
      className={cn(
        "text-xl leading-tight font-semibold tracking-tight",
        isMono && "font-mono tabular-nums",
      )}
    >
      {name}
    </h2>
  );
}

/**
 * The small facts, in a row that wraps.
 *
 * The panel is resizable, so a fixed two-column grid either wastes half the width or clips at the
 * narrow end. Wrapping keeps every fact readable at any width the person drags it to.
 */
function Facts({ children }: { children: React.ReactNode }) {
  return <div className="mt-2.5 flex flex-wrap gap-x-4 gap-y-1 text-xs">{children}</div>;
}

function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <span className="text-muted-foreground">
      {label} <span className="font-medium text-foreground">{children}</span>
    </span>
  );
}

/** One pill, one dot, one answer. */
function CoverPill({ cover }: { cover: Cover }) {
  return (
    <p
      className={cn(
        "mt-2.5 inline-flex max-w-full items-center gap-1.5 rounded-full border px-2.5 py-0.5 text-xs font-medium",
        cover.state === "in" && "border-aui-success/40 bg-aui-success/10 text-aui-success",
        cover.state === "out" && "border-destructive/40 bg-destructive/10 text-destructive",
        cover.state === "unknown" && "border-border bg-muted text-muted-foreground",
      )}
    >
      <span aria-hidden className="size-1.5 shrink-0 rounded-full bg-current" />
      <span>{cover.label}</span>
      <span aria-hidden className="opacity-40">
        ·
      </span>
      <span className="font-normal opacity-90">{cover.detail}</span>
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
