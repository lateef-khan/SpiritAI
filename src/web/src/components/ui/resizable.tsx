import * as React from "react";
import { Group, Panel, Separator } from "react-resizable-panels";

import { cn } from "@/lib/utils";

/**
 * shadcn's `resizable`, rewritten against react-resizable-panels 4.
 *
 * The published shadcn file targets version 2, whose parts were named `PanelGroup`, `Panel` and
 * `PanelResizeHandle`. Version 4 renamed them to `Group`, `Panel` and `Separator`, moved layout
 * persistence into `useDefaultLayout`, and replaced the `data-panel-group-direction` styling hook
 * with `data-separator`, so the registry copy neither compiles nor styles. The exported names stay
 * shadcn's, so the call sites read like every other shadcn component here.
 *
 * Only a horizontal group is styled, because that is the only one this app has. A column of panels
 * would need the handle's line and hit area turned on their side.
 */
function ResizablePanelGroup(props: React.ComponentProps<typeof Group>) {
  return <Group data-slot="resizable-panel-group" {...props} />;
}

/**
 * The library puts `className` on a nested div, and gives that div `overflow: auto` as an inline
 * style. A class cannot outrank an inline style, so a panel holding a child that scrolls itself
 * has to turn that off through `style` instead.
 */
function ResizablePanel({ style, ...props }: React.ComponentProps<typeof Panel>) {
  return <Panel data-slot="resizable-panel" style={{ overflow: "hidden", ...style }} {...props} />;
}

function ResizableHandle({ className, ...props }: React.ComponentProps<typeof Separator>) {
  return (
    <Separator
      data-slot="resizable-handle"
      className={cn(
        "relative w-px bg-border outline-none transition-colors",
        // One pixel is too thin to aim at. The pseudo-element widens only the pointer target and
        // leaves the drawn line a hairline.
        "after:absolute after:inset-y-0 after:left-1/2 after:w-3 after:-translate-x-1/2",
        "data-[separator=hover]:bg-ring data-[separator=active]:bg-ring data-[separator=focus]:bg-ring",
        className,
      )}
      {...props}
    />
  );
}

export { ResizablePanelGroup, ResizablePanel, ResizableHandle };
export { useDefaultLayout } from "react-resizable-panels";
