import type { ExportedMessageRepository } from "@assistant-ui/react";
import { describe, expect, it } from "vitest";

import type { HistoryPage } from "@/lib/history";
import { prependOlderPage, reloadNewestPage } from "./transcriptPages";

/**
 * How the two kinds of page land on a held transcript: the older one the reader scrolled up
 * for goes in ahead; the newest one, read again, goes over the rows it covers and no further.
 */

function said(id: string, parentId: string | null): ExportedMessageRepository["messages"][number] {
  return {
    parentId,
    message: {
      id,
      role: "user",
      content: [{ type: "text", text: id }],
      createdAt: new Date("2026-09-12T12:00:00Z"),
      attachments: [],
      metadata: { custom: {} },
    },
  };
}

/** The ids and parents of a repository, in order, which is all a page's shape comes down to. */
function chainOf(repository: ExportedMessageRepository): [string, string | null][] {
  return repository.messages.map((item) => [item.message.id, item.parentId]);
}

/** Turns [m4, m5] hang off nothing: the host nulls a window's first parent. */
const newest: HistoryPage = {
  repository: { headId: "m5", messages: [said("m4", null), said("m5", "m4")] },
  nextCursor: "before-m4",
};

const older: ExportedMessageRepository = { messages: [said("m2", null), said("m3", "m2")] };

describe("prependOlderPage", () => {
  it("hangs the held root off the page's last message and keeps the head and cursor", () => {
    const merged = prependOlderPage(newest, older);

    expect(chainOf(merged.repository)).toEqual([
      ["m2", null],
      ["m3", "m2"],
      ["m4", "m3"],
      ["m5", "m4"],
    ]);
    expect(merged.repository.headId).toBe("m5");
    expect(merged.nextCursor).toBe("before-m4");
  });

  it("leaves a transcript that already holds the page as it is", () => {
    const once = prependOlderPage(newest, older);

    expect(prependOlderPage(once, older)).toBe(once);
  });
});

describe("reloadNewestPage", () => {
  const held = prependOlderPage(newest, older);

  it("keeps the older rows above a page that starts among the held ones", () => {
    const fresh: HistoryPage = {
      repository: {
        headId: "m6",
        messages: [said("m5", null), said("m6", "m5")],
      },
      nextCursor: "before-m5",
    };

    const reloaded = reloadNewestPage(held, fresh);

    expect(chainOf(reloaded.repository)).toEqual([
      ["m2", null],
      ["m3", "m2"],
      ["m4", "m3"],
      ["m5", "m4"],
      ["m6", "m5"],
    ]);
    expect(reloaded.repository.headId).toBe("m6");
    expect(reloaded.nextCursor).toBe("before-m4");
  });

  it("takes a page that shares no row with the held ones as the whole transcript", () => {
    const fresh: HistoryPage = {
      repository: { headId: "m9", messages: [said("m8", null), said("m9", "m8")] },
      nextCursor: "before-m8",
    };

    expect(reloadNewestPage(held, fresh)).toBe(fresh);
  });

  it("takes a page that starts at the held root as the whole transcript", () => {
    const fresh: HistoryPage = {
      repository: { headId: "m6", messages: [said("m2", null), said("m6", "m2")] },
      nextCursor: null,
    };

    expect(reloadNewestPage(held, fresh)).toBe(fresh);
  });
});
