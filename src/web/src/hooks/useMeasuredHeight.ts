import { useCallback, useEffect, useRef, useState } from "react";

/**
 * Measures one element's height, live, for space to reserve elsewhere.
 *
 * `ResizeObserver` rather than a one-time read: the element may grow after it mounts — a
 * composer grows with its draft. The thread uses it to keep its sticky footer from covering the
 * last message, since the virtualizer's own "end" is its total row size, not the scroll box's
 * `scrollHeight`.
 *
 * @returns A ref callback to put on the element, and its current height in pixels — 0 until an
 *   element is attached.
 */
export function useMeasuredHeight(): [(el: HTMLElement | null) => void, number] {
  const [height, setHeight] = useState(0);

  const observerRef = useRef<ResizeObserver | null>(null);

  const setElement = useCallback((el: HTMLElement | null) => {
    observerRef.current?.disconnect();
    observerRef.current = null;

    if (!el) return;

    setHeight(el.getBoundingClientRect().height);

    const observer = new ResizeObserver(([entry]) => {
      if (entry) setHeight(entry.borderBoxSize?.[0]?.blockSize ?? entry.contentRect.height);
    });

    observer.observe(el);
    observerRef.current = observer;
  }, []);

  useEffect(() => () => observerRef.current?.disconnect(), []);

  return [setElement, height];
}
