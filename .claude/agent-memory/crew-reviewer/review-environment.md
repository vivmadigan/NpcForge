---
name: review-environment
description: How to run git, the build and the checks on this Windows machine so review evidence is real
metadata:
  type: reference
---

- `git` is not on the Bash tool's PATH here. Run git and dotnet through the PowerShell tool.
- An incremental `dotnet build` that compiles nothing prints "0 Warning(s)" whatever the code looks like. For the evidence line, run `dotnet build --no-incremental` so the warning count is real.
- After `dotnet test`, check that no MCP server is left running: `Get-Process -Name NpcForge.Server` and `Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'"` filtered on `*NpcForge.Server*`. At step 5 none were left: the server exits when its stdin pipe closes, even though `McpToolSource` never disposes the client.
- The done-when for step 5 is checked with `git diff step-4 -- NpcForge.Console/AgentLoop.cs`. The tag `step-4` exists (5946ead). Later steps may add more tags.

Related: [[review-false-positives]].
