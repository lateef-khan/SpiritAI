# What is ours and what is not

`runtime/` and `auth/` are ours. Everything in them is written and maintained here.

`components/` is almost all theirs. `ui/`, `assistant-ui/`, `icons/` and `elements/` came through
the shadcn and assistant-ui registries, as did `hooks/use-mobile.ts` and `lib/utils.ts`. **Prefer
not to edit them.** They carry no explanatory comments on purpose: a diff against upstream is the
cheap way to see whether one has been tampered with. `components/chat/` and `components/unit/` are
ours, and ours always goes in a named folder — a file loose at the top of `components/` belongs to
neither side and is the one shape to avoid.

`elements/` is worth naming, because its own path is already an edit. The registry writes every
`elements-*` item to `components/assistant-ui/elements/`; all thirteen were moved up to
`components/elements/` instead, and `sources.tsx` was left behind at the upstream path. So a fresh
`shadcn add elements-<name>` lands in the wrong directory and has to be moved by hand.

Three things have already broken the clean-overwrite promise:

- `a970b77` ran Prettier over the whole web codebase and reformatted all 27 files under `ui/` and
  `assistant-ui/`. None is byte-identical to what the CLI wrote any more, so the upstream diff is
  noise rather than the tamper check it was meant to be.
- `day-separator.tsx` and `sources.tsx` have bodies we changed; the other eleven under `elements/`
  still match upstream once whitespace is ignored.
- `thread.tsx` and `tool-fallback.tsx` are the only registry files under `assistant-ui/` whose
  bodies we edited, to hang our pieces into the render tree.

Four files under `assistant-ui/` are ours outright and never came from any registry: `draft.tsx`,
`search.tsx`, `regenerate.tsx` and `speaker.tsx`. They exist for two reasons, and both are worth
knowing before writing a fifth.

The registry elements are **presentational**. `DraftRestore` takes `onRestore` and `onDismiss` and
knows nothing about storage; `RegenerateMenu` takes a list of options and an `onPick`. Something has
to hold the state and talk to the runtime, and that something is the file in `assistant-ui/`.

The registry elements are also **built for the gallery**, so several render a whole conversation from
an array — `SpeakerIdentity` takes `turns`, `DaySeparator` takes `messages`. Inside
`ThreadPrimitive.Messages` assistant-ui already owns the message list, so a component that draws all
the messages cannot be nested in a loop over them. `DayDivider` is the single-message version, added
beside the original in the same file; `speaker.tsx` is the same move for `SpeakerIdentity`.

To refresh them:

    npx shadcn@latest add https://r.assistant-ui.com/thread.json
    npx shadcn@latest add https://r.assistant-ui.com/threadlist-sidebar.json
    npx shadcn@latest add https://ui.shadcn.com/r/styles/new-york-v4/utils.json
    npx shadcn@latest add label

The CLI resolves `@/components` by reading `../tsconfig.json` — the solution file, not
`tsconfig.app.json`. That is why the solution file carries a `compilerOptions.paths` entry it never
compiles with. Remove it and the next `shadcn add` writes its files into a directory literally named
`@`.

Radix is the primitive library here, not Base UI. assistant-ui publishes both flavours and the
registry's default entries resolved to Radix on their own; nothing was chosen by hand.

# Pages

Three documents, not routes: `index.html` (the chat app), `login.html` (sign-in) and `widget.html`
(the embeddable bubble). `vite.config.ts` builds each one, and moving between them is a navigation.

`auth/` gates the first from the second. Sign-in is Neon Auth — Managed Better Auth — and it needs
`VITE_NEON_AUTH_URL`; copy `.env.example` to `.env` and fill it in, or the app throws on load with
that instruction.

The gate is a courtesy, not a defence: it runs in the browser, on code a visitor can edit. Nothing
yet checks a token on `/v1`, so the conversation endpoint is still open to anyone who asks it
directly.
