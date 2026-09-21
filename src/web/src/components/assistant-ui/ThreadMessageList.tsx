"use client";

import { NEAR_TOP_ROWS, useOlderMessages, type OlderMessagesSource } from "@/lib/history";
import {
  ThreadPrimitive,
  unstable_useThreadMessageIds,
  useAui,
  useAuiEvent,
  useAuiState,
} from "@assistant-ui/react";
import {
  useVirtualizer,
  type ScrollToOptions,
  type VirtualItem,
  type Virtualizer,
} from "@tanstack/react-virtual";
import {
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ComponentProps,
  type ComponentType,
  type FC,
} from "react";

/** How long a glide to a sent message may keep re-aiming before it gives up and leaves the reader be. */
const GLIDE_MS = 1500;

/**
 * A guessed row height, before the virtualizer has measured anything.
 */
const ESTIMATED_ROW_HEIGHT = 96;

/**
 * The vertical space between messages, reproduced as padding rather than a flex `gap`.
 */
const ROW_GAP_PX = 24;

/**
 * How far from the end of the list, in pixels, still counts as being there.
 */
const AT_END_PX = 80;

/**
 * A sent message taller than this is pinned by its foot rather than its head, so the reply under
 * it starts on screen.
 */
const TALL_ANCHOR_PX = 160;

const TALL_ANCHOR_VISIBLE_PX = 96;

/**
 * Whether a row that changed size should move the scroll position by the change, so what is on
 * screen stays put.
 */
function keepsViewInPlace(
  item: VirtualItem,
  _delta: number,
  instance: Virtualizer<HTMLElement, Element>,
): boolean {
  const fold = (instance.scrollOffset ?? 0) + instance.scrollAdjustments;
  return instance.itemSizeCache.has(item.key) ? item.end <= fold : item.start < fold;
}

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
 */
export const ThreadMessageList = forwardRef<ThreadMessageListHandle, ThreadMessageListProps>(
  function ThreadMessageList(
    { scrollElement, paddingEnd, Message, onAtEndChange, olderMessages },
    ref,
  ) {
    const ids = unstable_useThreadMessageIds();

    const count = ids.length;

    const aui = useAui();

    const isRunning = useAuiState((s) => s.thread.isRunning);

    const [listElement, setListElement] = useState<HTMLDivElement | null>(null);

    const [scrollMargin, setScrollMargin] = useState(0);

    const empty = count === 0;

    useLayoutEffect(() => {
      setScrollMargin(listElement?.offsetTop ?? 0);
    }, [listElement, empty]);

    const [anchorId, setAnchorId] = useState<string | null>(null);

    const anchorIndex = useMemo(
      () => (anchorId === null ? -1 : ids.indexOf(anchorId)),
      [ids, anchorId],
    );

    const pinned = anchorIndex >= 0;

    useEffect(() => {
      if (anchorId !== null && !pinned) setAnchorId(null);
    }, [anchorId, pinned]);

    const reserveRef = useRef(0);

    const followingEnd = !(pinned && (isRunning || reserveRef.current > 0));

    const virtualizer = useVirtualizer({
      count,
      getScrollElement: () => scrollElement,
      estimateSize: () => ESTIMATED_ROW_HEIGHT + ROW_GAP_PX,
      getItemKey: (index) => ids[index]!,
      anchorTo: "end",
      followOnAppend: false,
      scrollEndThreshold: followingEnd ? AT_END_PX : -1,
      scrollMargin,
      overscan: 6,
      paddingEnd: empty ? 0 : paddingEnd,
    });

    virtualizer.shouldAdjustScrollPositionOnItemSizeChange = keepsViewInPlace;

    const items = virtualizer.getVirtualItems();

    const measurements = virtualizer.measurementsCache;

    const anchor = pinned ? measurements[anchorIndex] : undefined;

    const last = count > 0 ? measurements[count - 1] : undefined;

    const anchorHeight = anchor ? anchor.size - ROW_GAP_PX : 0;

    const pinTarget = anchor
      ? anchor.start + (anchorHeight > TALL_ANCHOR_PX ? anchorHeight - TALL_ANCHOR_VISIBLE_PX : 0)
      : null;

    const viewportHeight = virtualizer.scrollRect?.height ?? 0;

    const reserve =
      pinTarget !== null && last
        ? Math.max(0, pinTarget + viewportHeight - last.end - paddingEnd)
        : 0;

    reserveRef.current = reserve;

    const atEnd = virtualizer.isAtEnd(AT_END_PX);

    useImperativeHandle(
      ref,
      () => ({
        isAtEnd: () => virtualizer.isAtEnd(AT_END_PX),
        scrollToEnd: (options) => virtualizer.scrollToEnd(options),
      }),
      [virtualizer],
    );

    const glideUntil = useRef(0);

    useAuiEvent("thread.runStart", () => {
      const messages = aui.thread.getState().messages;
      let sent = messages.length - 1;
      while (sent >= 0 && messages[sent]!.role !== "user") sent -= 1;
      if (sent < 0) return;
      glideUntil.current = Date.now() + GLIDE_MS;
      setAnchorId(messages[sent]!.id);
    });

    const gliding = () => glideUntil.current !== 0;

    useFollowAppend({ virtualizer, lastId: ids[count - 1] ?? null, count, atEnd, gliding });

    useLayoutEffect(() => {
      if (!gliding() || pinTarget === null) return;
      const at = scrollElement?.scrollTop ?? 0;
      if (Math.abs(at - pinTarget) <= 1 || Date.now() > glideUntil.current) {
        glideUntil.current = 0;
        return;
      }
      virtualizer.scrollToOffset(pinTarget, { behavior: "smooth" });
    });

    const firstRenderedIndex = items[0]?.index ?? null;

    const rangeOffset = virtualizer.scrollOffset ?? 0;

    const isRangeCurrent = useCallback(
      () => scrollElement !== null && rangeOffset === scrollElement.scrollTop,
      [rangeOffset, scrollElement],
    );

    const lastReportedAtEnd = useRef<boolean | null>(null);

    useEffect(() => {
      if (lastReportedAtEnd.current === atEnd) return;
      lastReportedAtEnd.current = atEnd;
      onAtEndChange?.(atEnd);
    }, [atEnd, onAtEndChange]);

    const components: ComponentProps<typeof ThreadPrimitive.Unstable_MessageById>["components"] =
      useMemo(() => ({ Message }), [Message]);

    return (
      <div
        ref={setListElement}
        data-slot="aui_message-group"
        style={{ position: "relative", height: virtualizer.getTotalSize() + reserve }}
      >
        {olderMessages ? (
          <OlderMessages
            source={olderMessages}
            firstRenderedIndex={firstRenderedIndex}
            messageCount={count}
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
              transform: `translateY(${item.start - scrollMargin}px)`,
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
 * Scrolls to the end when a message is appended while the reader was there.
 */
function useFollowAppend({
  virtualizer,
  lastId,
  count,
  atEnd,
  gliding,
}: {
  virtualizer: { scrollToEnd: (options?: Pick<ScrollToOptions, "behavior">) => void };
  lastId: string | null;
  count: number;
  atEnd: boolean;
  gliding: () => boolean;
}) {
  const previous = useRef({ lastId, count, atEnd });

  useLayoutEffect(() => {
    const before = previous.current;
    previous.current = { lastId, count, atEnd };

    const appended = lastId !== before.lastId && count > before.count;
    if (appended && before.atEnd && !gliding()) virtualizer.scrollToEnd();
  });
}

/**
 * Asks for older pages as the reader nears the top, and says so while one is in flight.
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
