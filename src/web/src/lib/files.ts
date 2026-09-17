import type { FileLinks } from "@/lib/sandboxLinks";

/**
 * One file a reply produced and the host kept, with the link the browser fetches it from.
 *
 * The same shape on a live turn (the `agentcore_file` frame) and on a restored one (the history's
 * `file` part). `url` is null when the store has no web door; the part still names the file so
 * the words can at least name it too.
 */
export type ReplyFile = {
  readonly name: string;
  readonly mediaType: string;
  readonly length: number;
  readonly url: string | null;
};

/**
 * Turns one kept file into the content part assistant-ui draws it as: a picture inline, anything
 * else as a download row. A file with no link is left out: there is nothing to draw but a name,
 * and the words already carry that.
 *
 * This is the only place that decides picture or download. The host sends facts, never a verdict.
 */
export function fileContent(file: ReplyFile) {
  if (!file.url) {
    return null;
  }

  if (file.mediaType.startsWith("image/") && !file.mediaType.includes("svg")) {
    return { type: "image" as const, image: file.url, filename: file.name };
  }

  return {
    type: "file" as const,
    data: file.url,
    mimeType: file.mediaType,
    filename: file.name,
    sourceType: "url" as const,
  };
}

/** The links these files can be fetched from, by name, for the words to point at. */
export function fileLinks(files: readonly ReplyFile[]): FileLinks {
  return new Map(files.flatMap((file) => (file.url ? [[file.name, file.url] as const] : [])));
}
