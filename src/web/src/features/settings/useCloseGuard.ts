import { useCallback, useEffect, useRef } from "react";

/** Lets a dialog ignore close requests (Escape, outside click, X, Cancel) while steps run. */
export function useCloseGuard(onOpenChange: (open: boolean) => void) {
  const busy = useRef(false);
  const setBusy = useCallback((value: boolean) => {
    busy.current = value;
  }, []);
  return {
    setBusy,
    onOpenChange: (open: boolean) => {
      if (!open && busy.current) return;
      onOpenChange(open);
    },
    contentProps: {
      onEscapeKeyDown: (event: KeyboardEvent) => {
        if (busy.current) event.preventDefault();
      },
      onInteractOutside: (event: Event) => {
        if (busy.current) event.preventDefault();
      },
    },
  };
}

export function useReportBusy(running: boolean, setBusy: (value: boolean) => void) {
  useEffect(() => {
    setBusy(running);
    return () => setBusy(false);
  }, [running, setBusy]);
}
