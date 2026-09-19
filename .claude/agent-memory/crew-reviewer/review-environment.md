---
name: review-environment
description: How to run git, the build and the checks on this Windows machine so review evidence is real
metadata:
  type: reference
---

- `git` is not on the Bash tool's PATH here. Run git and dotnet through the PowerShell tool.
- An incremental `dotnet build` that compiles nothing prints "0 Warning(s)" whatever the code looks like. For the evidence line, run `dotnet build --no-incremental` so the warning count is real, then `dotnet test --no-build`.
- After `dotnet test`, check that no MCP server is left running: `Get-Process -Name NpcForge.Server` and `Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'"` filtered on `*NpcForge.Server*`. None were left at steps 5 and 6; since step 6 `McpToolSource` is `IAsyncDisposable` and the tests use `await using`.
- Step tags exist: `step-4` (5946ead), `step-5`. Diff a finished step against the previous tag, e.g. `git diff step-4 -- NpcForge.Console/AgentLoop.cs` for "the loop runs unchanged".
- `git diff` excludes a folder with `-- . ':(exclude).claude'` (crew tooling edits are not step code).

Related: [[review-false-positives]].
