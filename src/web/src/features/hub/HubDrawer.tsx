/** The drawer of tiles: one per app the Person has, with the one-time Desk alerts hint on its tile. */
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

import { AppIcon } from "./HubAppIcon";

export function HubDrawer({
  tiles,
  opened,
  onOpenTile,
  showDeskHint,
  onDismissDeskHint,
}: {
  tiles: readonly { id: string; name: string; url: string }[];
  opened: ReadonlySet<string>;
  onOpenTile: (id: string) => void;
  showDeskHint: boolean;
  onDismissDeskHint: () => void;
}) {
  return (
    <section
      aria-label="Apps"
      className="absolute inset-0 flex flex-col items-center overflow-auto bg-background px-4 py-14"
    >
      <h1 className="text-xl font-semibold">Spirit Hub</h1>
      <p className="mb-7 text-muted-foreground">Pick an app.</p>

      <div className="grid w-full max-w-md grid-cols-[repeat(auto-fill,120px)] justify-center gap-3">
        {tiles.map((tile) => (
          <div key={tile.id} className="flex flex-col items-center gap-1">
            <Button
              variant="ghost"
              onClick={() => onOpenTile(tile.id)}
              className="h-auto flex-col gap-2 rounded-lg border border-transparent px-2 py-4 hover:border-border"
            >
              <AppIcon id={tile.id} className="size-14 rounded-2xl [&>svg]:size-7" />
              <span className="font-semibold">{tile.name}</span>
              <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
                <span
                  className={cn(
                    "size-1.5 rounded-full",
                    opened.has(tile.id) ? "bg-aui-success" : "bg-border",
                  )}
                />
                {opened.has(tile.id) ? "Open" : "Not open yet"}
              </span>
            </Button>

            {tile.id === "desk" && showDeskHint ? (
              <p className="max-w-[120px] text-center text-[11px] text-muted-foreground">
                <b className="text-foreground">Turn on alerts:</b>{" "}
                <Button
                  asChild
                  variant="link"
                  className="h-auto p-0 text-[11px] text-muted-foreground underline"
                >
                  <a href={tile.url} target="_blank" rel="noreferrer" onClick={onDismissDeskHint}>
                    Open Desk
                  </a>
                </Button>
              </p>
            ) : null}
          </div>
        ))}
      </div>
    </section>
  );
}
