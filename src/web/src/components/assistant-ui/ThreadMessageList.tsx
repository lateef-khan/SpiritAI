"use client";

import { ThreadPrimitive, unstable_useThreadMessageIds, useAuiEvent } from "@assistant-ui/react";
import { useVirtualizer, type ScrollToOptions } from "@tanstack/react-virtual";
import {
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  type ComponentProps,
  type ComponentType,
  type FC,
} from "react";
import { NEAR_TOP_ROWS, useOlderMessages, type OlderMessagesSource } from "@/lib/history";

/** How long a glide to the end may keep re-aiming before it gives up and leaves the reader be. */
const GLIDE_MS = 1500;

/**
 * A guessed row height, before the virtualizer has measured anything.
 *
 * Only the first paint of the first render for a size the reader never sees settle — every row
 * carries `ref={measureElement}`, so the real height replaces this the moment a row mounts.
 */
const ESTIMATED_ROW_HEIGHT = 96;

/**
 * The vertical space between messages, reproduced as padding rather than a flex `gap` — an
 * absolutely positioned row has no siblings for `gap-y-6` to sit between. Matches that class's
 * `1.5rem`.
 */
const ROW_GAP_PX = 24;

export type ThreadMessageListHandle = {
  isAtEnd: () => boolean;
  scrollToEnd: (options?: Pick<ScrollToOptions, "behavior">) => void;
};

export type ThreadMessageListProps = {
  /** The DOM node `ThreadPrimitive.Viewport` scrolls — the virtualizer measures against it. */
  scrollElement: HTMLElement | null;
  /** The composer/footer's measured height, kept out from under the last message. */
  paddingEnd: number;
  /** The row component — `ThreadMessage` from thread.tsx, dispatching on role. */
  Message: ComponentType;
  /**
   * Told whenever whether the reader is at the bottom changes, so a "scroll to bottom" button
   * elsewhere in the tree can show or hide itself without polling this component.
   */
  onAtEndChange?: ((atEnd: boolean) => void) | undefined;
  /**
   * Where the pages before the oldest loaded message come from. Without one the list shows what
   * the runtime holds and never asks for more.
   */
  olderMessages?: OlderMessagesSource | undefined;
};

/**
 * The thread's messages, virtualized.
 *
 * Rows are keyed by message id (`getItemKey`), not by index — the id is stable across a prepend,
 * so a row already on screen keeps its DOM node (and, for OpenUI forms, its focus and scroll
 * position) when 100 older messages appear above it.
 *
 * `anchorTo: "end"` with `followOnAppend: true` is the only anchor: the list follows the bottom
 * while the reader is there, and a turn streaming in never has to fight a second anchor that
 * also wants the scroll position.
 */
export const ThreadMessageList = forwardRef<ThreadMessageListHandle, ThreadMessageListProps>(
  function ThreadMessageList(
    { scrollElement, paddingEnd, Message, onAtEndChange, olderMessages },
    ref,
  ) {
    const ids = unstable_useThreadMessageIds();

    const virtualizer = useVirtualizer({
      count: ids.length,
      getScrollElement: () => scrollElement,
      estimateSize: () => ESTIMATED_ROW_HEIGHT + ROW_GAP_PX,
      getItemKey: (index) => ids[index]!,
      anchorTo: "end",
      followOnAppend: true,
      scrollEndThreshold: 80,
      overscan: 6,
      paddingEnd,
    });

    useImperativeHandle(
      ref,
      () => ({
        isAtEnd: () => virtualizer.isAtEnd(),
        scrollToEnd: (options) => virtualizer.scrollToEnd(options),
      }),
      [virtualizer],
    );

    // A reader who sent from part-way up glides down to their own message. Smooth through the
    // virtualizer and not through CSS: the virtualizer skips its scroll corrections while a smooth
    // scroll of its own is in flight, and would fight one it did not start. One glide is aimed at
    // the end as it was when the run started; the reply then grows under it and it lands short,
    // outside the follow threshold. So the aim is renewed on every render until the end is reached
    // or the glide has had its time.
    const glideUntil = useRef(0);
    useAuiEvent("thread.runStart", () => {
      glideUntil.current = Date.now() + GLIDE_MS;
      virtualizer.scrollToEnd({ behavior: "smooth" });
    });
    useEffect(() => {
      if (glideUntil.current === 0) return;
      if (virtualizer.isAtEnd() || Date.now() > glideUntil.current) {
        glideUntil.current = 0;
        return;
      }
      virtualizer.scrollToEnd({ behavior: "smooth" });
    });

    const items = virtualizer.getVirtualItems();
    const firstRenderedIndex = items[0]?.index ?? null;
    // Where the range above was computed. The element's `scrollTop` moves the moment anything
    // scrolls it — the virtualizer pinning a fresh list to its end, say — and the range only
    // follows on the render after; equal means the range is of where the box is.
    const rangeOffset = virtualizer.scrollOffset ?? 0;
    const isRangeCurrent = useCallback(
      () => scrollElement !== null && rangeOffset === scrollElement.scrollTop,
      [rangeOffset, scrollElement],
    );
    // The virtualizer re-renders this component on every scroll tick (it subscribes to the
    // scroll element itself), so reading `isAtEnd()` here is already live — it just needs
    // forwarding to whatever draws the button, which lives outside this subtree.
    const atEnd = virtualizer.isAtEnd();
    const lastReportedAtEnd = useRef<boolean | null>(null);
    useEffect(() => {
      if (lastReportedAtEnd.current === atEnd) return;
      lastReportedAtEnd.current = atEnd;
      onAtEndChange?.(atEnd);
    }, [atEnd, onAtEndChange]);

    // `Unstable_MessageById` compares `components` by identity, so a fresh object per render
    // would remount every row's subtree on every scroll frame.
    const components: ComponentProps<typeof ThreadPrimitive.Unstable_MessageById>["components"] =
      useMemo(() => ({ Message }), [Message]);

    return (
      <div
        data-slot="aui_message-group"
        style={{ position: "relative", height: virtualizer.getTotalSize() }}
      >
        {olderMessages ? (
          <OlderMessages
            source={olderMessages}
            firstRenderedIndex={firstRenderedIndex}
            messageCount={ids.length}
            atEnd={atEnd}
            isRangeCurrent={isRangeCurrent}
          />
        ) : null}
        {items.map((item) => (
          <div
            key={item.key}
            data-index={item.index}
            ref={virtualizer.measureElement}
            style={{
              position: "absolute",
              top: 0,
              left: 0,
              width: "100%",
              transform: `translateY(${item.start}px)`,
              paddingBottom: ROW_GAP_PX,
            }}
          >
            <ThreadPrimitive.Unstable_MessageById
              messageId={ids[item.index]!}
              components={components}
            />
          </div>
        ))}
      </div>
    );
  },
);

/**
 * Asks for older pages as the reader nears the top, and says so while one is in flight.
 *
 * Its own component rather than a hook in the list: the loader runs a TanStack Query, which
 * needs a `QueryClientProvider` above it, and a list with no source to page from — a test, a
 * runtime that holds everything already — should not need one.
 */
const OlderMessages: FC<{
  source: OlderMessagesSource;
  firstRenderedIndex: number | null;
  messageCount: number;
  atEnd: boolean;
  isRangeCurrent: () => boolean;
}> = ({ source, firstRenderedIndex, messageCount, atEnd, isRangeCurrent }) => {
  const { isFetchingOlder } = useOlderMessages({
    source,
    firstRenderedIndex,
    messageCount,
    atEnd,
    isRangeCurrent,
  });

  if (!isFetchingOlder || firstRenderedIndex === null || firstRenderedIndex > NEAR_TOP_ROWS) {
    return null;
  }

  return (
    <div
      role="status"
      className="text-muted-foreground absolute top-0 flex w-full justify-center py-2 text-xs"
    >
      Loading older messages…
    </div>
  );
};
