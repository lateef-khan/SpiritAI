import { ComposerPrimitive } from "@assistant-ui/react";
import { ArrowUpIcon } from "lucide-react";

import { Button } from "@/components/ui/button";

import { useHandoffComposer } from "./HandoffComposerContext";

// Matches the thread composer's shell and input classes (`thread.tsx`'s `Composer`), minus the
// attachment dropzone and the attachment row this box never carries.
const SHELL_CLASSNAME =
  "flex w-full flex-col gap-2 rounded-(--composer-radius) border bg-(--composer-bg) p-(--composer-padding)";
const INPUT_CLASSNAME =
  "aui-composer-input caret-primary placeholder:text-muted-foreground/60 max-h-48 min-h-10 w-full resize-none bg-transparent px-2.5 py-1 text-base leading-6 outline-none";

/**
 * The handoff reply box, rendered in the thread's `Composer` slot.
 *
 * A handoff moves through three states and this box reads that state off `HandoffComposerContext`
 * rather than props, since it is mounted by identity as a slot with none: a live box once the
 * viewer holds the chat, a disabled one naming why otherwise, and once the chat is back with
 * Spirit, no box at all.
 */
export function HandoffComposer() {
  const { handoff, canReply, sendError } = useHandoffComposer();

  if (handoff.status === "done") {
    return (
      <p className="text-sm text-muted-foreground">
        This chat is back with Spirit. The next message gets a normal bot answer.
      </p>
    );
  }

  const disabled = !canReply;

  const placeholder = handoff.status === "waiting" ? "Take the chat to reply." : "Reply…";

  const footer = canReply
    ? handoff.email
      ? `This reply also goes to ${handoff.email}`
      : "Enter sends"
    : handoff.status === "waiting"
      ? "Only the person who takes the chat can reply."
      : `${handoff.assignee?.name ?? "Someone"} has this chat.`;

  return (
    <div className="flex w-full flex-col gap-1">
      <ComposerPrimitive.Root className="relative flex w-full flex-col">
        <div data-slot="aui_composer-shell" className={SHELL_CLASSNAME}>
          <ComposerPrimitive.Input
            placeholder={placeholder}
            className={INPUT_CLASSNAME}
            rows={1}
            autoFocus
            aria-label="Reply"
            disabled={disabled}
          />
          <div className="flex items-center justify-between gap-2">
            <p className="text-xs text-muted-foreground">{footer}</p>
            <ComposerPrimitive.Send asChild>
              <Button type="button" variant="default" size="icon" aria-label="Send">
                <ArrowUpIcon className="size-4" />
              </Button>
            </ComposerPrimitive.Send>
          </div>
        </div>
      </ComposerPrimitive.Root>
      {sendError ? <p className="text-xs text-destructive">{sendError}</p> : null}
    </div>
  );
}
