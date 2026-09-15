import { createLibrary } from "@openuidev/react-lang";
import { openuiLibrary } from "@openuidev/react-ui";
import { openuiChatLibrary } from "@openuidev/react-ui/genui-lib";

/**
 * The single vocabulary the Renderer draws and the skill teaches, built from
 * the pinned packages at import time.
 *
 * The chat library's `Card` root, `FollowUpBlock`, `ListBlock`, and
 * `SectionBlock` win over the full library's versions; `Stack` and `Modal`
 * survive from the full library so stored `root = Stack(...)` history still
 * draws.
 */
export const spiritChatLibrary = createLibrary({
  components: Object.values({
    ...openuiLibrary.components,
    ...openuiChatLibrary.components,
  }),
  root: "Card",
  id: "spirit-chat",
});
