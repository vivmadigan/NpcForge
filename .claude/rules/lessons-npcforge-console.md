---
paths:
  - "NpcForge.Console/**"
  - "NpcForge.Tests/**"
  - "docs/runs/**"
  - "NpcForge.Server/**"
---

# Lessons: NpcForge.Console

Curated, project-specific traps in this project. Two lines each; the full account —
symptom, dead ends, why it works here — is in the linked file under `docs/lessons/`.

## [001 OpenAI rejects function tools on Chat Completions when reasoning is on](../../docs/lessons/001-openai-responses-endpoint.md)
A reasoning OpenAI model 400s when `ChatOptions.Tools` is set on Chat Completions; forcing
`reasoning_effort` to `none` "fixes" it but leaks a vendor workaround into shared `ChatOptions`.
Build the OpenAI client on the Responses endpoint instead — `IChatClient` absorbs the swap with
no change to `ChatAgent.cs` or `AgentLoop.cs`.

## [004 A test asserting on "Tool failed:" can pass for the wrong reason](../../docs/lessons/004-tool-failed-prefix-hides-why.md)
Every `IToolSource` failure returns the same `"Tool failed:"` prefix, so a test that only checks
the prefix (and a name substring) can pass even with the filter code it targets reverted.
Give the call arguments that would genuinely succeed, so the filter is the only thing left that
can make it fail.

## [005 Redirecting a run to a file in PowerShell reorders the trace against the answer](../../docs/lessons/005-powershell-redirect-reorders-trace.md)
`> file 2>&1` and `*>` both scramble the stdout/stderr interleaving in PowerShell, and not the
same way twice — this matters because the ordering of `[loop]`/`[app]` traces against the answer
is the information docs/runs/ exists to capture.
Capture through `cmd /c "... > file 2>&1"` instead, or paste the console buffer for runs watched
live under F5.

## [006 A run and its --load look different in a console paste when the save is exact](../../docs/lessons/006-console-paste-not-byte-exact.md)
Visual Studio's debug console flattens typographic punctuation to ASCII and a terminal paste can
silently drop trailing Markdown hard-break spaces, so a run and its `--load` can look different
by eye even when `characters.json` holds the run byte for byte.
Diff the printed text line by line against the saved file (or trust the round-trip test); don't
compare two consoles by eye.
