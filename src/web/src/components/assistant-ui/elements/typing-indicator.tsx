"use client";

import type { ComponentProps } from "react";
import { cn } from "@/lib/utils";
import { paper } from "./surfaces";

const DOT_DELAYS = ["-0.32s", "-0.16s", "0s"];

/**
 * Three bouncing dots, and nothing else. Decorative: the text beside them says who is typing.
 */
export function TypingDots({ className, ...props }: Omit<ComponentProps<"span">, "children">) {
  return (
    <span
      aria-hidden
      data-slot="typing-dots"
      className={cn("inline-flex items-center gap-1 align-middle", className)}
      {...props}
    >
      {DOT_DELAYS.map((delay) => (
        <span
          key={delay}
          className="bg-current/40 size-1.5 animate-bounce rounded-full motion-reduce:animate-none"
          style={{ animationDelay: delay, animationDuration: "1.1s" }}
        />
      ))}
    </span>
  );
}

export function TypingIndicator({
  variant = "bubble",
  className,
  ...props
}: Omit<ComponentProps<"div">, "children" | "variant" | "role" | "aria-label"> & {
  variant?: "bubble" | "bare";
}) {
  if (variant === "bare") {
    return (
      <div
        data-slot="typing-indicator"
        data-variant="bare"
        role="status"
        aria-label="Assistant is typing"
        className={cn("flex", className)}
        {...props}
      >
        <TypingDots className="text-foreground" />
      </div>
    );
  }

  return (
    <div
      data-slot="typing-indicator"
      data-variant="bubble"
      className={cn(paper, "w-fit rounded-full px-4 py-3.5", className)}
      {...props}
    >
      <div role="status" aria-label="Assistant is typing" className="flex">
        <TypingDots className="text-foreground" />
      </div>
    </div>
  );
}
