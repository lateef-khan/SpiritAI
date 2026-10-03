import type { Virtualizer } from "@tanstack/react-virtual";

/** Spacer rows stand in for the rows that are not drawn, so the scroll height stays right. */
export function spacersOf(virtualizer: Virtualizer<HTMLElement, Element>) {
  const items = virtualizer.getVirtualItems();
  return {
    items,
    above: items.length > 0 ? items[0]!.start : 0,
    below: items.length > 0 ? virtualizer.getTotalSize() - items[items.length - 1]!.end : 0,
  };
}

export function SpacerRow({ height, colSpan }: { height: number; colSpan: number }) {
  if (height <= 0) return null;
  return (
    <tr aria-hidden>
      <td colSpan={colSpan} style={{ height, padding: 0 }} />
    </tr>
  );
}
