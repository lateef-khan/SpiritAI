import { vi } from "vitest";

/**
 * What every test gets before it runs.
 *
 * happy-dom lays nothing out: every box is 0 by 0. `@tanstack/react-virtual` reads the scroll
 * box's height to decide which rows to draw, so in a test it would draw none. This stand-in
 * draws every row at its guessed height instead. The library's own arithmetic is the library's
 * to test; what a test here holds in place is what the rows say once drawn.
 */
vi.mock("@tanstack/react-virtual", () => ({
  useVirtualizer: (
    options: {
      count: number;
      estimateSize: (index: number) => number;
      getItemKey?: (index: number) => string | number;
    } & Record<string, unknown>,
  ) => {
    const { count, estimateSize } = options;

    const items = Array.from({ length: count }, (_, index) => ({
      index,
      key: options.getItemKey?.(index) ?? index,
      start: index * estimateSize(index),
      size: estimateSize(index),
      end: (index + 1) * estimateSize(index),
      lane: 0,
    }));

    return {
      getVirtualItems: () => items,
      measurementsCache: items,
      getTotalSize: () => count * estimateSize(0),
      measureElement: () => {},
      // Every row is already drawn, so "the bottom" is always on screen.
      isAtEnd: () => true,
      scrollToEnd: () => {},
      scrollToIndex: () => {},
      scrollToOffset: () => {},
      scrollRect: null,
      scrollOffset: 0,
      options,
    };
  },
}));
