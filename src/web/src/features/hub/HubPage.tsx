/**
 * The Hub: a banner, a drawer of tiles, and one frame per opened app.
 *
 * Every opened app keeps its own `<iframe>` for the life of the page — never removed, never
 * remounted — so switching apps and coming back never loses what was on screen. A hidden frame is
 * `visibility: hidden`, not unmounted, for the same reason: the mock's own note is that a hidden
 * frame keeps its real size, so nothing inside it ever draws itself at 0×0.
 *
 * All the state this draws — which tiles exist, which are open, what each frame's address is right
 * now — lives in `useHubFrames`. This file only lays it out.
 */
import { LayoutGrid, LogOut, UserRound } from "lucide-react";

import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { cn } from "@/lib/utils";

import { useSession } from "../auth/authClient";
import { AppIcon } from "./HubAppIcon";
import { HubDrawer } from "./HubDrawer";
import { HubFrame } from "./HubFrame";
import { isHubAppId } from "./hubMessages";
import { useHubFrames } from "./useHubFrames";

export function HubPage() {
  const hub = useHubFrames();
  const currentTile = hub.tiles.find((tile) => tile.id === hub.current) ?? null;
  const drawerOpen = hub.current === null;

  return (
    <TooltipProvider>
      <div className="flex h-dvh w-full flex-col bg-background text-foreground">
        <HubBanner
          current={currentTile}
          drawerOpen={drawerOpen}
          onToggleDrawer={hub.toggleDrawer}
          onSignOut={hub.signOut}
        />

        <main className="relative min-h-0 flex-1">
          {hub.tiles
            .filter((tile) => hub.opened.has(tile.id))
            .map((tile) => (
              <HubFrame
                key={tile.id}
                name={tile.name}
                src={hub.frameSrc[tile.id]}
                active={hub.current === tile.id}
                loaded={hub.firstLoaded.has(tile.id)}
                onLoad={() => hub.markLoaded(tile.id)}
                failed={isHubAppId(tile.id) && hub.failed.has(tile.id)}
                onRetry={() => {
                  if (isHubAppId(tile.id)) hub.retry(tile.id);
                }}
              />
            ))}

          {drawerOpen ? (
            hub.loadFailed ? (
              <HubLoadError onRetry={hub.retryLoadingApps} />
            ) : (
              <HubDrawer
                tiles={hub.tiles}
                opened={hub.opened}
                onOpenTile={hub.openTile}
                showDeskHint={hub.showDeskHint}
                onDismissDeskHint={hub.dismissDeskHint}
              />
            )
          ) : null}
        </main>
      </div>
    </TooltipProvider>
  );
}

function HubLoadError({ onRetry }: { onRetry: () => void }) {
  return (
    <section
      aria-label="Apps"
      className="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-background p-6 text-center"
    >
      <p className="font-medium">Could not load your apps.</p>
      <Button onClick={onRetry}>Try again</Button>
    </section>
  );
}

function HubBanner({
  current,
  drawerOpen,
  onToggleDrawer,
  onSignOut,
}: {
  current: { id: string; name: string } | null;
  drawerOpen: boolean;
  onToggleDrawer: () => void;
  onSignOut: () => void;
}) {
  const user = useSession().data?.user;
  const name = user?.name?.trim() || user?.email;

  return (
    <header className="flex h-12 flex-none items-center gap-2.5 border-b bg-sidebar px-2.5">
      <Tooltip>
        <TooltipTrigger asChild>
          <Button
            variant="ghost"
            size="icon"
            aria-label="Apps"
            aria-expanded={drawerOpen}
            onClick={onToggleDrawer}
            className={cn(drawerOpen && "bg-accent text-primary")}
          >
            <LayoutGrid />
          </Button>
        </TooltipTrigger>
        <TooltipContent>Apps</TooltipContent>
      </Tooltip>

      <div className="flex min-w-0 flex-1 items-center gap-2 font-semibold">
        {current ? (
          <>
            <AppIcon id={current.id} className="size-6" />
            <span className="truncate">{current.name}</span>
          </>
        ) : (
          <span>Spirit Hub</span>
        )}
      </div>

      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" size="icon" aria-label="Account">
            <UserRound />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="min-w-56">
          {user ? (
            <>
              <DropdownMenuLabel className="grid font-normal leading-tight">
                <span className="truncate font-medium">{name}</span>
                <span className="truncate text-xs text-muted-foreground">{user.email}</span>
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
            </>
          ) : null}
          <DropdownMenuItem variant="destructive" onSelect={onSignOut}>
            <LogOut />
            Sign out
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </header>
  );
}
