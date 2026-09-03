import type { OrderDocument, UnitHeader } from "@/api/types.gen";
import { Skeleton } from "@/components/ui/skeleton";

import { asDay, asSerial } from "./format";

/**
 * The facts that stay pinned above the tabs.
 *
 * Pinned because the serial and the dates are what staff check most often, and because a
 * 142-line parts list must never be able to bury them.
 */
export function UnitFacts({ header }: { header: UnitHeader }) {
  return (
    <div className="border-b px-3 py-2.5">
      <h2 className="truncate text-sm font-semibold">{header.modelName ?? header.modelNo}</h2>

      <p className="mt-0.5 truncate text-xs text-muted-foreground">
        {[header.category, `model ${header.modelNo}`, header.isSole ? "Sole" : null]
          .filter(Boolean)
          .join(" · ")}
      </p>

      <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-xs">
        <Fact label="Serial">
          <span className="font-mono tabular-nums">{asSerial(header.serial)}</span>
        </Fact>
        <Fact label="Bought">{asDay(header.purchasedOn)}</Fact>
        <Fact label="Set up">{asDay(header.setUpOn)}</Fact>
        <Fact label="Built">{header.manufacturedOn ?? "—"}</Fact>
      </dl>
    </div>
  );
}

/** The same shell for a work order, which has no tabs under it. */
export function OrderFacts({ order }: { order: OrderDocument }) {
  return (
    <div className="border-b px-3 py-2.5">
      <h2 className="truncate font-mono text-sm font-semibold tabular-nums">{order.orderNumber}</h2>

      <p className="mt-0.5 truncate text-xs text-muted-foreground">
        {order.orderType ?? "Work order"}
      </p>

      <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-3 gap-y-1 text-xs">
        <Fact label="Raised">{asDay(order.orderedOn)}</Fact>
        <Fact label="Booked">{asDay(order.appointedOn)}</Fact>
        <Fact label="Shipped">{asDay(order.shippedOn)}</Fact>
        <Fact label="Closed">{asDay(order.closedOn)}</Fact>
        <Fact label="Tech">{order.technician ?? "—"}</Fact>
        <Fact label="ISP">{order.ispName ?? "—"}</Fact>
        <Fact label="Tracking">
          <span className="font-mono tabular-nums">{order.trackingNo ?? "—"}</span>
        </Fact>
      </dl>

      {order.notes ? <p className="mt-2 text-xs text-muted-foreground">{order.notes}</p> : null}
    </div>
  );
}

/** The pinned header while it is still being read. */
export function FactsSkeleton() {
  return (
    <div className="space-y-2 border-b px-3 py-2.5">
      <Skeleton className="h-4 w-32" />
      <Skeleton className="h-3 w-40" />
      <Skeleton className="h-3 w-48" />
    </div>
  );
}

function Fact({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="truncate">{children}</dd>
    </>
  );
}
