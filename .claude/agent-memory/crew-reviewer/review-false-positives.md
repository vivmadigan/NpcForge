---
name: review-false-positives
description: Things in NpcForge that look like findings but are project convention or expected build output; do not report them
metadata:
  type: feedback
---

Do not report these in NpcForge reviews:

- Unused `using System; using System.Collections.Generic; using System.Text;` at the top of classes, and block-scoped `namespace X { }` next to file-scoped ones. They come from the Visual Studio "Add class" template and are in nearly every file (AgentLoop, IToolSource, FakeToolSource, McpToolSource, CharacterTools). The project's rules do not cover them.
- `NpcForge.Server.exe`, `.deps.json` and `.runtimeconfig.json` (but no `NpcForge.Server.dll`) inside `NpcForge.Console/bin` and `NpcForge.Tests/bin`. The build-order `ProjectReference ... ReferenceOutputAssembly="false"` (step 5) copies these. No types cross; the real server runs from `NpcForge.Server/bin` through `dotnet run --project`.
- Empty `.mcp/` and `Tools/` folders in `NpcForge.Server`, left after trimming the MCP template. Git does not track empty folders.
- Whitespace: tabs next to spaces in csproj files, double blank lines, a blank line before a closing brace, "No newline at end of file". No rule covers any of it.
- `NpcForge.Server/characters.json` modified in the working tree (step 8 on). It is the user's saved runs, data not code, and is committed on purpose. The server is `Microsoft.NET.Sdk`, so the file is not copied to bin.
- A test-only optional constructor parameter (`McpToolSource(string? charactersPath = null)`). PLAN.md step 8 decided it; it is a parameter, not an abstraction.

**Why:** found while reviewing steps 5 to 8. Each one looked like a finding at first, and none can be grounded in AGENTS.md or BUILD.md.

**How to apply:** skip these unless a project rule changes. Related: [[review-recurring-patterns]].
