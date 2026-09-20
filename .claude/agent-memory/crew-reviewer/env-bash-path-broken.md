---
name: env-bash-path-broken
description: The Bash tool in this repo starts with a Windows-format PATH that sh cannot parse, so git/dotnet/ls all fail with "command not found" until PATH is re-exported
metadata:
  type: reference
---

The Bash tool here inherits the Windows `PATH` verbatim, semicolons and all. POSIX `sh`
splits on `:`, so the whole thing resolves to one nonsense entry and even `ls` fails with
`command not found` (exit 127).

Shell state does not persist between Bash calls, so every call needs the prefix:

```
export PATH="/usr/bin:/bin:/c/Program Files/Git/cmd:/c/Program Files/dotnet"
```

That covers coreutils, `git` and `dotnet`, which is everything a review needs. Add
`/c/Program Files/GitHub CLI` if `gh` is required.

**How to apply:** first line of every Bash invocation in this project, before `cd`. The
PowerShell tool has no such problem, so it is the cheaper choice for one-off commands;
Bash is still better for `grep`/`awk`/`od` pipelines.
