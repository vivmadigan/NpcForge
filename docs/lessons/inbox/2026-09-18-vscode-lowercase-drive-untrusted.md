# VS Code chat treats the repo as untrusted because it spells the drive c:

- **Status:** active
- **Date:** 2026-09-18
- **Plan version:** v01
- **Applies to:** .claude/skills/crew/**, .claude/settings.json, ~/.claude.json (outside the repo) · tags: claude-code, vscode, trust

## Symptom
In the VS Code chat panel, `/` listed no `crew:*` skills and no crew agents were available. `/reload-plugins` printed `Reloaded: 2 plugins · 12 skills · 6 agents · 0 hooks` (the two were the synced `engineering` and `cowork-plugin-management`). Nothing in the chat said why. The extension log did:
`Ignoring 10 permissions.allow entries from .claude/settings.json: this workspace has not been trusted. Run Claude Code interactively here once and accept the trust dialog, or set projects["c:/Users/camer/source/repos/NpcForge"].hasTrustDialogAccepted: true in C:\Users\camer\.claude.json.`

## What did not work
- `/reload-plugins`, twice. Same counts: it reloads plugins but does not re-check trust.
- Blaming the version. `claude --version` on PATH is 2.1.23 and `claude plugin list` says "No plugins installed", but the extension does not use that binary. It bundles 2.1.276 at `~/.vscode/extensions/anthropic.claude-code-2.1.276-win32-x64/resources/native-binary/claude.exe`, which supports skills-directory plugins (added May 2026).
- Checking the plugin layout and `plugin.json` against the docs. All correct: the bundled binary, run from a terminal at the repo root, lists `crew@skills-dir ... Status: √ loaded`.
- Reproducing with a lowercase cwd from a terminal (`Start-Process -WorkingDirectory "c:\Users\..."`, then `plugin list`). Still loaded, so the case theory looked wrong. Only the extension's own session misses the trust.
- `/cd C:\Users\camer\source\repos\NpcForge` to move the session onto the trusted spelling. VS Code replies `/cd isn't available in this environment.`

## What worked
Adding the lowercase key to `projects` in `~/.claude.json`, beside the existing `C:\...` and `C:/...` entries, then restarting:
```json
"c:/Users/camer/source/repos/NpcForge": { "hasTrustDialogAccepted": true }
```
The next extension log shows `Found 3 plugins`, `Total plugin agents loaded: 5`, `Registered 2 hooks from 3 plugins`, and no "not been trusted" line.

## Why it works here, specifically
VS Code hands the extension the folder as `c:\Users\...` with a lowercase drive, and the trust lookup keys on that exact spelling. Trust for this repo had only been accepted in terminal sessions, which recorded `C:\...` and `C:/...`. No trust dialog appeared in the VS Code panel. The crew is a project-scope `@skills-dir` plugin, which loads only in a trusted workspace, and the same gate drops the project's `permissions.allow` rules. So both vanished without a message in the chat.
It fails the other way too. With only the lowercase entry for a new repo (HarnessLab), `plugin list` from a terminal said "1 project-scope directory under ./.claude/skills/ that may load as a plugin was skipped because this workspace was not trusted". Each spelling needs its own entry: `c:/...` for VS Code, `C:/...` for the terminal.

## How to tell it is happening again
`this workspace has not been trusted` in `%APPDATA%\Code\logs\<timestamp>\window1\exthost\Anthropic.claude-code\Claude VSCode.log`, or `/reload-plugins` reporting `0 hooks`.
