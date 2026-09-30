/**
 * State and wiring for the Hub's frames: which tiles are open, which one is on screen, the address
 * each open frame is showing, and sign-out across every open app. The tile list lives in
 * `useHubTiles` and the sign-in loop guard in `useHubSignIn`, each next to the state it owns.
 *
 * `HubPage` only reads this and draws it; every rule about *when* a frame's address changes lives
 * here.
 */
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

import type { HubTile } from "@/api/types.gen";

import { authClient } from "../auth/authClient";
import { loginUrlWithReturnTo } from "../auth/routes";
import { isHubAppId, isHubMessage, type HubAppId } from "./hubMessages";
import { useHubSignIn } from "./useHubSignIn";
import { useHubTiles } from "./useHubTiles";

/** How long the Hub waits for an app's `hub:signed-out` before signing out of Spirit anyway. */
const SIGN_OUT_TIMEOUT_MS = 3_000;

const DESK_HINT_SEEN_KEY = "spirit-hub-desk-hint-seen";

function hasSeenDeskHint(): boolean {
  try {
    return window.localStorage.getItem(DESK_HINT_SEEN_KEY) === "1";
  } catch {
    return false;
  }
}

function rememberDeskHintSeen(): void {
  try {
    window.localStorage.setItem(DESK_HINT_SEEN_KEY, "1");
  } catch {
    // A full quota or private browsing loses only the reminder, not the click that dismissed it.
  }
}

/** The origin an app's tile is served from — where its sign-out page and `postMessage`s live. */
function tileOrigin(tile: HubTile): string {
  return new URL(tile.url, window.location.origin).origin;
}

interface SignOutInFlight {
  pending: Set<HubAppId>;
  timer: ReturnType<typeof setTimeout> | null;
}

export interface UseHubFrames {
  readonly tiles: readonly HubTile[];
  readonly loadFailed: boolean;
  readonly opened: ReadonlySet<string>;
  readonly current: string | null;
  readonly frameSrc: Readonly<Record<string, string>>;
  readonly failed: ReadonlySet<HubAppId>;
  readonly firstLoaded: ReadonlySet<string>;
  readonly showDeskHint: boolean;
  retryLoadingApps: () => void;
  openTile: (id: string) => void;
  toggleDrawer: () => void;
  markLoaded: (id: string) => void;
  retry: (app: HubAppId) => void;
  signOut: () => void;
  dismissDeskHint: () => void;
}

export function useHubFrames(): UseHubFrames {
  const { tiles, tilesLoaded, loadFailed, retryLoadingApps } = useHubTiles();

  const [openedState, setOpenedState] = useState<Set<string>>(new Set());
  const [current, setCurrent] = useState<string | null>(null);
  const [lastTile, setLastTile] = useState<string | null>(null);
  const [frameSrcState, setFrameSrc] = useState<Record<string, string>>({});
  const [firstLoaded, setFirstLoaded] = useState<Set<string>>(new Set());
  const [deskHintSeen, setDeskHintSeen] = useState(hasSeenDeskHint);

  const deskTile = tilesLoaded ? (tiles.find((tile) => tile.id === "desk") ?? null) : null;

  // Desk loads at start so its alerts work before anyone opens it (the mock's own rule): a grant
  // made in a top-level Desk tab only reaches this frame if the frame already exists. Derived
  // rather than written into state by an effect, so there is exactly one source of truth for "is
  // Desk open" and no render where it is briefly missing while an effect catches up.
  const opened = useMemo(() => {
    if (!deskTile || openedState.has("desk")) return openedState;
    return new Set(openedState).add("desk");
  }, [openedState, deskTile]);

  const frameSrc = useMemo(() => {
    if (!deskTile || "desk" in frameSrcState) return frameSrcState;
    return { ...frameSrcState, desk: deskTile.url };
  }, [frameSrcState, deskTile]);

  const tilesRef = useRef(tiles);
  const openedRef = useRef(opened);
  const signOutState = useRef<SignOutInFlight | null>(null);

  const { failed, requestSignIn, retry } = useHubSignIn(setFrameSrc);

  // Declared ahead of every other effect below: those read `tilesRef`/`openedRef` synchronously
  // from inside an effect of their own, in the same commit as a `tiles`/`opened` update, and
  // effects run in declaration order, so these two have to go first to hand them the fresh value.
  useEffect(() => {
    tilesRef.current = tiles;
  }, [tiles]);

  useEffect(() => {
    openedRef.current = opened;
  }, [opened]);

  const openTile = useCallback((id: string) => {
    const tile = tilesRef.current.find((t) => t.id === id);
    if (!tile) return;

    setOpenedState((prev) => (prev.has(id) ? prev : new Set(prev).add(id)));
    setFrameSrc((prev) => (id in prev ? prev : { ...prev, [id]: tile.url }));
    setCurrent(id);
    setLastTile(id);
  }, []);

  const toggleDrawer = useCallback(() => {
    setCurrent((prev) => (prev !== null ? null : lastTile));
  }, [lastTile]);

  const markLoaded = useCallback((id: string) => {
    setFirstLoaded((prev) => (prev.has(id) ? prev : new Set(prev).add(id)));
  }, []);

  const finishSignOut = useCallback(() => {
    const state = signOutState.current;
    if (state?.timer) clearTimeout(state.timer);
    signOutState.current = null;

    void authClient
      .signOut()
      .catch((error: unknown) => {
        console.error("[hub] sign-out failed", error);
      })
      .finally(() => {
        window.location.replace(loginUrlWithReturnTo("/"));
      });
  }, []);

  const signOut = useCallback(() => {
    if (signOutState.current) return;

    const pending = new Set<HubAppId>();
    for (const id of openedRef.current) {
      if (!isHubAppId(id)) continue;
      const tile = tilesRef.current.find((t) => t.id === id);
      if (!tile) continue;

      pending.add(id);
      const signOutUrl = `${tileOrigin(tile)}/spirit/sign-out`;
      setFrameSrc((prev) => ({ ...prev, [id]: signOutUrl }));
    }

    if (pending.size === 0) {
      finishSignOut();
      return;
    }

    signOutState.current = { pending, timer: setTimeout(finishSignOut, SIGN_OUT_TIMEOUT_MS) };
  }, [finishSignOut]);

  const dismissDeskHint = useCallback(() => {
    rememberDeskHintSeen();
    setDeskHintSeen(true);
  }, []);

  useEffect(() => {
    if (!tilesLoaded) return;
    const wanted = window.location.hash.slice(1);
    if (wanted) openTile(wanted);
  }, [tilesLoaded, openTile]);

  // The address keeps naming the last app shown even while the drawer is open, not just while an
  // app is on screen, so reloading the page (F5) comes back to that app instead of the drawer.
  useEffect(() => {
    if (!tilesLoaded) return;
    const shown = current ?? lastTile;
    const wanted = shown ? `#${shown}` : window.location.pathname + window.location.search;
    try {
      window.history.replaceState(null, "", wanted);
    } catch {
      // Not fatal: the frame still shows the right app, only the address bar falls behind.
    }
  }, [current, lastTile, tilesLoaded]);

  // Escape only ever closes the drawer back to whatever app was last shown — it never opens one
  // that was never opened, since there is nothing to close back to.
  useEffect(() => {
    function onKeyDown(event: KeyboardEvent) {
      if (event.key !== "Escape") return;
      if (current !== null || lastTile === null) return;
      setCurrent(lastTile);
    }

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [current, lastTile]);

  useEffect(() => {
    function onMessage(event: MessageEvent) {
      const data: unknown = event.data;
      if (!isHubMessage(data)) return;

      if (data.type === "hub:needs-sign-in") {
        if (event.origin !== window.location.origin) return;
        requestSignIn(data.app);
        return;
      }

      const tile = tilesRef.current.find((t) => t.id === data.app);
      if (!tile || event.origin !== tileOrigin(tile)) return;

      const state = signOutState.current;
      if (!state) return;
      state.pending.delete(data.app);
      if (state.pending.size === 0) finishSignOut();
    }

    window.addEventListener("message", onMessage);
    return () => window.removeEventListener("message", onMessage);
  }, [requestSignIn, finishSignOut]);

  return {
    tiles,
    loadFailed,
    opened,
    current,
    frameSrc,
    failed,
    firstLoaded,
    showDeskHint: !deskHintSeen && tiles.some((tile) => tile.id === "desk"),
    retryLoadingApps,
    openTile,
    toggleDrawer,
    markLoaded,
    retry,
    signOut,
    dismissDeskHint,
  };
}
