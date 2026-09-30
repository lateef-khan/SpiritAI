/**
 * One opened app's `<iframe>`, plus whatever covers it right now: nothing, the first-open spinner,
 * or the sign-in loop's failure message. The iframe itself is always in the tree once the tile is
 * opened — an overlay covers it rather than replacing it, so a failure or a hidden tab never tears
 * it down and loses the app's state.
 */
import { LoaderIcon } from "lucide-react";

import { Button } from "@/components/ui/button";

export function HubFrame({
  name,
  src,
  active,
  loaded,
  onLoad,
  failed,
  onRetry,
}: {
  name: string;
  src: string;
  active: boolean;
  loaded: boolean;
  onLoad: () => void;
  failed: boolean;
  onRetry: () => void;
}) {
  return (
    <div className="absolute inset-0" style={{ visibility: active ? "visible" : "hidden" }}>
      <iframe
        title={name}
        src={src}
        onLoad={onLoad}
        className="absolute inset-0 size-full border-0 bg-background"
      />

      {failed ? (
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-background p-6 text-center">
          <p className="font-medium">Could not sign in to {name}</p>
          <Button onClick={onRetry}>Try again</Button>
        </div>
      ) : active && !loaded ? (
        <div
          className="absolute inset-0 flex items-center justify-center bg-background text-muted-foreground"
          aria-busy
        >
          <div className="flex flex-col items-center gap-2">
            <LoaderIcon className="size-7 animate-spin" />
            Opening…
          </div>
        </div>
      ) : null}
    </div>
  );
}
