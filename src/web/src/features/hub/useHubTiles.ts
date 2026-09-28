/**
 * The Hub's tile list: fetched once from the server, and retried on request rather than left as an
 * unhandled rejection when the fetch fails outright.
 */
import { useCallback, useEffect, useState } from "react";

import { listHubApps } from "@/api/sdk.gen";
import type { HubTile } from "@/api/types.gen";

export interface UseHubTiles {
  readonly tiles: readonly HubTile[];
  readonly tilesLoaded: boolean;
  readonly loadFailed: boolean;
  retryLoadingApps: () => void;
}

export function useHubTiles(): UseHubTiles {
  const [tiles, setTiles] = useState<HubTile[]>([]);
  const [tilesLoaded, setTilesLoaded] = useState(false);
  const [loadFailed, setLoadFailed] = useState(false);
  const [attempt, setAttempt] = useState(0);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      try {
        const { data } = await listHubApps({ throwOnError: true });
        if (cancelled) return;
        setTiles(data.tiles);
        setTilesLoaded(true);
      } catch {
        if (cancelled) return;
        setLoadFailed(true);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [attempt]);

  const retryLoadingApps = useCallback(() => {
    setLoadFailed(false);
    setAttempt((n) => n + 1);
  }, []);

  return { tiles, tilesLoaded, loadFailed, retryLoadingApps };
}
