/**
 * The model links a file it wrote as `sandbox:/<path>/<name>`. The host asks every model for that
 * form, whatever vendor runs it, and leaves it in the words on purpose: the model reads its own
 * replies back next turn, and a route or a signed link in there would be a thing for it to learn.
 * So the browser is where the link becomes something a click can open.
 *
 * Only the last path segment is the name. The directory is the vendor's: OpenAI writes to
 * `/mnt/data`, others elsewhere, and a model may write the path it really used.
 */

/** Where a file the reply produced can be fetched from, by the name the model gave it. */
export type FileLinks = ReadonlyMap<string, string>;

/** `sandbox:` then any directories, capturing the file name after the last slash. */
const SandboxPath = String.raw`sandbox:(?:\/[^\s)"'<>/]+)*\/([^\s)"'<>/]+)`;

const SandboxLink = new RegExp(SandboxPath, "g");

const WrappedSandboxLink = new RegExp(String.raw`!?\[([^\]]*)\]\(${SandboxPath}\)`, "g");

/**
 * Points every sandbox link in the text at the file's real link.
 *
 * A file the host kept gets its link. A file the host did not keep (refused by the policy, or
 * lost) has nowhere to point, so the markdown link is unwrapped to its label: `[chart](sandbox:…)`
 * becomes `chart`, and a bare `sandbox:…` becomes the file's name. Nothing is left that a click
 * could follow to a dead end.
 *
 * @param text The reply's words, markdown included.
 * @param links What the host linked this turn.
 * @returns The same words, with no `sandbox:` link left in them.
 */
export function resolveSandboxLinks(text: string, links: FileLinks): string {
  if (!text.includes("sandbox:/")) {
    return text;
  }

  // Wrapped links first, so an unkept file loses its brackets and not only its target.
  const unwrapped = text.replace(WrappedSandboxLink, (whole, label: string, name: string) =>
    links.has(decodeURIComponent(name)) ? whole : label || decodeURIComponent(name),
  );

  return unwrapped.replace(SandboxLink, (_whole, name: string) => {
    const decoded = decodeURIComponent(name);
    return links.get(decoded) ?? decoded;
  });
}
