import { useQueryClient } from "@tanstack/react-query";

import * as Events from "@/features/handoff/events";
import { useSocketEvents } from "@/lib/realtime/SocketProvider";

import { reviveHandoff } from "../api/handoffsApi";
import {
  applyClaimed,
  applyDone,
  applyMessage,
  applyReconnect,
  applyWaiting,
} from "../cache/handoffCache";

/**
 * Keeps the handoff cache current off the staff socket, for as long as the caller is mounted.
 *
 * Every push is applied to the cache through `handoffCache.ts` — a row moved in place, a count
 * nudged — and a reconnect marks everything stale, so a push lost while the socket was down is
 * caught up. Mount it once, somewhere that is up on every screen: the sidebar's counts need the
 * pushes whether or not the inbox is open.
 *
 * @param meKey The signed-in caller's key, as `callerKeyOf` builds it.
 */
export function useHandoffPushes(meKey: string): void {
  const cache = useQueryClient();

  useSocketEvents({
    onOpen: () => applyReconnect(cache),
    on: {
      [Events.Waiting]: (row: Events.WaitingPush) => applyWaiting(cache, reviveHandoff(row)),
      [Events.Claimed]: (push: Events.ClaimedPush) => applyClaimed(cache, push, meKey),
      [Events.Done]: (push: Events.DonePush) => applyDone(cache, push, meKey),
      [Events.MessageCreated]: (message: Events.MessagePush) => applyMessage(cache, message, meKey),
    },
  });
}
