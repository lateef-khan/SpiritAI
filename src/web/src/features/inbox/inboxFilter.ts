import type { HandoffFilter, HandoffOwner } from "./api/handoffsApi";
import type { InboxTab, InboxView } from "./hooks/useHandoffs";

/**
 * How the inbox's three controls — the Open/Done switch, the Mine/Unassigned/All tabs, and the
 * order toggle — read as one listing filter, and what each control does to it.
 *
 * The done view has no unassigned rows, since a closed handoff was claimed first, so a filter
 * that would ask for them there asks for everyone's instead. Each view has its own natural
 * order: the queue reads oldest first, the way it is served, and the closed chats read newest
 * first, the way a sent-mail folder does.
 */

const ownerOfTab: Record<InboxTab, HandoffOwner> = {
  mine: "me",
  unassigned: "none",
  all: "all",
};

const tabOfOwner: Record<HandoffOwner, InboxTab> = {
  me: "mine",
  none: "unassigned",
  all: "all",
};

/** The listing a view opens on. */
export function defaultFilter(view: InboxView): HandoffFilter {
  return { view, owner: "all", order: view === "open" ? "oldest" : "newest" };
}

/** The tab a filter is showing. */
export function tabOf(filter: HandoffFilter): InboxTab {
  return tabOfOwner[filter.owner];
}

/** Switches views, keeping the tab where the new view has it and taking the view's own order. */
export function withView(filter: HandoffFilter, view: InboxView): HandoffFilter {
  return {
    ...defaultFilter(view),
    owner: view === "done" && filter.owner === "none" ? "all" : filter.owner,
  };
}

/** Switches tabs within the view. */
export function withTab(filter: HandoffFilter, tab: InboxTab): HandoffFilter {
  return { ...filter, owner: ownerOfTab[tab] };
}

/** Turns the listing the other way up. */
export function reversed(filter: HandoffFilter): HandoffFilter {
  return { ...filter, order: filter.order === "oldest" ? "newest" : "oldest" };
}
