# What is ours and what is not

`runtime/` and `auth/` are ours. Everything in them is written and maintained here, and they are
the only places a change to this application's behaviour belongs.

`components/`, `hooks/`, and `lib/` are assistant-ui's and shadcn's. Those files arrived through
their registries and are byte-identical to what the CLI wrote. **Do not edit them.** They carry no
explanatory comments for the same reason: a diff against upstream is the cheap way to see whether
one has been tampered with, and re-running the commands below has to stay a clean overwrite rather
than a merge.

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
