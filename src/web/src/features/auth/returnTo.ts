/**
 * A backslash is a path separator to some browsers' URL parser (`/\evil.com` resolves as
 * `//evil.com`), and a control character can smuggle a scheme past the `startsWith("/")` check.
 */
function hasBackslashOrControlChar(value: string): boolean {
  for (let i = 0; i < value.length; i++) {
    const code = value.charCodeAt(i);
    if (value[i] === "\\" || code <= 0x1f || code === 0x7f) return true;
  }
  return false;
}

/** Where the sign-in page may send a Person back to: a path on this origin, or the Hub. */
export function safeReturnTo(raw: string | null, origin: string): string {
  if (!raw || !raw.startsWith("/") || raw.startsWith("//")) return "/";
  if (hasBackslashOrControlChar(raw)) return "/";

  let url: URL;
  try {
    url = new URL(raw, origin);
  } catch {
    return "/";
  }
  if (url.origin !== origin) return "/";

  // `URL` collapses dot segments before this point, and a leading one can turn a path that never
  // said "//" into one that does — `/.//evil.com` and `/a/..//evil.com` both parse to pathname
  // `//evil.com`, which a browser reads as protocol-relative to `evil.com`. Run the same guards on
  // what comes out as were run on what went in.
  const path = url.pathname + url.search + url.hash;
  return path.startsWith("//") || path.startsWith("/\\") ? "/" : path;
}
