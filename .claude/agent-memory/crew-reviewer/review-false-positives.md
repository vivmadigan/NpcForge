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
- Tabs next to spaces in csproj files. Whitespace only, and no rule covers it.

**Why:** found while reviewing step 5 (2026-09-19). Each one looked like a finding at first, and none can be grounded in AGENTS.md or BUILD.md.

**How to apply:** skip these unless a project rule changes. Related: [[review-recurring-patterns]].
