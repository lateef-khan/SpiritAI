import { useEffect, useRef } from "react";
import { useVirtualizer } from "@tanstack/react-virtual";

import { Skeleton } from "@/components/ui/skeleton";

import type { Handoff } from "../api/handoffsApi";
import { HandoffRow } from "./HandoffRow";

/** A row's height before it is measured. Close enough that the scrollbar does not jump. */
const RowGuess = 84;

/** How many rows past the visible ones are drawn, so a fast scroll does not show blanks. */
const Overscan = 8;

/**
 * The rows, drawn only where the eye is.
 *
 * Only the rows in view, and a few either side, are in the DOM; the rest are one tall spacer.
 * A row's true height is measured once it is drawn, since a reason line makes one taller than
 * the next; the height is filed under the row's id rather than its place in the list, so a sort
 * or a push that moves rows about does not hand one row another's height. When there are pages
 * still unread, one extra slot follows the last row: a placeholder that, the moment it scrolls
 * into view, asks for the next page.
 */
export function HandoffList({
  rows,
  hasMore,
  loadMore,
  selectedId,
  onSelect,
}: {
  rows: Handoff[];
  hasMore: boolean;
  loadMore: () => void;
  selectedId: number | null;
  onSelect: (row: Handoff) => void;
}) {
  const scroller = useRef<HTMLDivElement>(null);

  const virtualizer = useVirtualizer({
    count: hasMore ? rows.length + 1 : rows.length,
    getScrollElement: () => scroller.current,
    getItemKey: (index) => rows[index]?.id ?? "more",
    estimateSize: () => RowGuess,
    overscan: Overscan,
  });

  const items = virtualizer.getVirtualItems();
  const lastDrawn = items[items.length - 1]?.index ?? -1;

  useEffect(() => {
    if (hasMore && lastDrawn >= rows.length) loadMore();
  }, [hasMore, lastDrawn, rows.length, loadMore]);

  return (
    <div ref={scroller} className="min-h-0 flex-1 overflow-y-auto">
      <ul className="relative w-full" style={{ height: virtualizer.getTotalSize() }}>
        {items.map((item) => {
          const row = rows[item.index];
          return (
            <li
              key={item.key}
              data-index={item.index}
              ref={virtualizer.measureElement}
              className="absolute top-0 left-0 w-full"
              style={{ transform: `translateY(${item.start}px)` }}
            >
              {row ? (
                <HandoffRow
                  handoff={row}
                  selected={row.id === selectedId}
                  onSelect={() => onSelect(row)}
                />
              ) : (
                <Skeleton className="m-3.5 h-14" />
              )}
            </li>
          );
        })}
      </ul>
    </div>
  );
}
