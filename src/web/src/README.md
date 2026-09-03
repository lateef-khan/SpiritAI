# What is ours and what is not

`runtime/` and `auth/` are ours. Everything in them is written and maintained here.

`components/` is shared ground, and the split is by folder:

- `components/ui/`, `components/assistant-ui/` and `components/icons/` arrived through the shadcn
  and assistant-ui registries. **Do not edit them.** They carry no explanatory comments on purpose:
  a diff against upstream is the cheap way to see whether one has been tampered with, and re-running
  the commands below should stay an overwrite rather than a merge. `hooks/use-mobile.ts` and
  `lib/utils.ts` came the same way and follow the same rule.
- Every other folder under `components/` is ours — `chat/`, `elements/` and `unit/` today. Ours
  always goes in a named folder. A file loose at the top of `components/` belongs to neither side
  and is the one shape to avoid.

Two things already broke that promise, and both matter before you re-run a registry command:

- `a970b77` ran Prettier over the whole web codebase and reformatted all 27 files under `ui/` and
  `assistant-ui/`. None of them is byte-identical to what the CLI wrote any more, so the diff
  against upstream is noise rather than the tamper check it was meant to be.
- Five files of ours were written straight into `assistant-ui/` instead of beside our own code:
  `draft.tsx`, `search.tsx`, `regenerate.tsx`, `speaker.tsx` and `elements/sources.tsx`. They never
  came from the registry at all. `thread.tsx` and `tool-fallback.tsx` are the only registry files
  whose bodies we edited, to hang our `elements/` pieces into the render tree.

So re-adding any of these is a merge, not an overwrite. `icons/` is untouched.

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
