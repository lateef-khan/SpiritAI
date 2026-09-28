/** The small tinted glyph a tile, or the banner's current app, draws for one of the Hub's apps. */
import { LayoutGrid, MessageSquare, Settings as SettingsIcon, Truck, Users } from "lucide-react";

import { cn } from "@/lib/utils";

const APP_ICONS: Record<string, typeof Truck> = {
  desk: Truck,
  crm: Users,
  chat: MessageSquare,
  settings: SettingsIcon,
};

const APP_TINTS: Record<string, string> = {
  desk: "bg-[oklch(0.55_0.16_255.5)]",
  crm: "bg-[oklch(0.55_0.14_300)]",
  chat: "bg-[oklch(0.6_0.13_170)]",
  settings: "bg-muted-foreground",
};

export function AppIcon({ id, className }: { id: string; className?: string }) {
  const Icon = APP_ICONS[id] ?? LayoutGrid;
  return (
    <div
      className={cn(
        "grid flex-none place-items-center rounded-md text-white",
        APP_TINTS[id] ?? "bg-muted-foreground",
        className,
      )}
    >
      <Icon className="size-3.5" />
    </div>
  );
}
