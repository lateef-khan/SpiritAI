/**
 * The signed-in person, in the bottom-left corner.
 *
 * A row carrying the avatar, the name and the email, which opens a menu above itself. The menu is
 * where sign-out lives: the corner is glanced at constantly and pressed rarely, so the one
 * destructive control on the page sits behind a deliberate second click rather than under the
 * cursor's resting place.
 */
import { ChevronsUpDown, LogOut } from "lucide-react";
import { useState } from "react";

import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { SidebarMenu, SidebarMenuButton, SidebarMenuItem } from "@/components/ui/sidebar";

import { authClient, useSession } from "./authClient";
import { forgetToken } from "./authFetch";
import { LOGIN_URL } from "./routes";

export function AccountMenu() {
  const { data } = useSession();
  const [signingOut, setSigningOut] = useState(false);
  const user = data?.user;

  // The menu sits inside AuthGate, so a missing user means the session is still being read. Drawing
  // an empty row and then filling it in would flicker the corner on every load.
  if (!user) return null;

  const name = user.name?.trim() || user.email;

  async function signOut() {
    if (signingOut) return;
    setSigningOut(true);

    try {
      await authClient.signOut();
    } catch (error) {
      console.error("[auth] sign-out failed", error);
    }

    forgetToken();
    // Sent to the login page whether or not Neon accepted the sign-out. A live session bounces
    // straight back to the app from there, so a failure shows itself instead of leaving a button
    // that silently does nothing.
    window.location.replace(LOGIN_URL);
  }

  return (
    <SidebarMenu>
      <SidebarMenuItem>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <SidebarMenuButton size="lg" className="gap-2">
              <AccountAvatar name={name} email={user.email} image={user.image} />
              <AccountLines name={name} email={user.email} />
              <ChevronsUpDown className="ml-auto size-4 text-muted-foreground" />
            </SidebarMenuButton>
          </DropdownMenuTrigger>
          <DropdownMenuContent
            side="top"
            align="start"
            sideOffset={4}
            className="w-(--radix-dropdown-menu-trigger-width) min-w-56 rounded-lg"
          >
            <DropdownMenuLabel className="flex items-center gap-2 p-2 font-normal">
              <AccountAvatar name={name} email={user.email} image={user.image} />
              <AccountLines name={name} email={user.email} />
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem variant="destructive" disabled={signingOut} onSelect={signOut}>
              <LogOut />
              Log out
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </SidebarMenuItem>
    </SidebarMenu>
  );
}

function AccountAvatar({
  name,
  email,
  image,
}: {
  name: string;
  email: string;
  image?: string | null;
}) {
  return (
    <Avatar className="rounded-lg">
      {image ? <AvatarImage src={image} alt="" className="rounded-lg" /> : null}
      <AvatarFallback className="rounded-lg text-xs font-medium">
        {initials(name, email)}
      </AvatarFallback>
    </Avatar>
  );
}

function AccountLines({ name, email }: { name: string; email: string }) {
  return (
    <div className="grid flex-1 text-left text-sm leading-tight">
      <span className="truncate font-medium">{name}</span>
      <span className="truncate text-xs text-muted-foreground">{email}</span>
    </div>
  );
}

/**
 * Up to two letters for the tile behind a missing photo. Magic-link sign-in carries no picture, so
 * this is what most people see rather than a fallback for the rare account.
 */
function initials(name: string, email: string): string {
  const words = name.split(/\s+/).filter(Boolean);

  if (words.length > 1) {
    return (words[0][0] + words[words.length - 1][0]).toUpperCase();
  }

  const source = words[0] ?? email;
  return source.slice(0, 2).toUpperCase();
}
