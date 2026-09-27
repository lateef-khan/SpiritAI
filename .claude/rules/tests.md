---
paths:
  - "tests/**"
  - "src/web/src/**/*.test.ts"
  - "src/web/src/**/*.test.tsx"
---

# Tests

When you finish adding or changing tests, run the `test-cleanup-audit` skill on the diff before you
report done.

## Database tests skip without a word

A test that opens `PostgresFixture` skips when no database is named. A green `dotnet test` then
proves nothing about it. To run it:

```bash
just spirit db-up
AgentCore__Secrets__postgres-connection-string="$(just spirit db-url)" dotnet test
```

The fixture also reads `POSTGRES_CONNECTION_STRING`. `secrets/dev.env` sets that one to Neon. Do
not run the tests with `dev.env` loaded: they write to the shared database.
