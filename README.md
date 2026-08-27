# SpiritAI

A chat application built on [AgentCore](https://github.com/MatthewHsu1/AgentCore).

## Quick start

Needs .NET 10, Node, and an AgentCore checkout at `../AgentCore` (sibling of this repo).

```bash
export OPENAI_API_KEY=sk-...
cd src/SpiritAI
dotnet run --launch-profile spirit
# open http://localhost:5299/chat
```

By default the app builds against the local AgentCore source (fast dev loop, debuggable).
Pass `-p:UseLocalAgentCore=false` to build against the published NuGet package instead.
See `CLAUDE.md` for the full story.
