# Run 01 — 2026-10-24 — the replay fixture

| | |
| --- | --- |
| Model | openai / `gpt-6-luna` |
| Setting | an inn in a major city |
| Players want | the name of a fence |
| Difficulty | `Wall` |
| Obstacle *(rolled, seed 1)* | `Wont` |
| Occupation | innkeeper |
| Seed | 1 |
| Skill | `skills/npc-writer/SKILL.md`, 2239 chars, `6b58d7c2` |
| Commit | `ac68ce8` on `feat/safety-net` (PR #2, squash-merged as `698291d`), clean tree |
| Tokens | turn 1: 708 in / 21 out · turn 2: 757 in / 2061 out |
| Transcript | `NpcForge.Tests/Fixtures/replay.jsonl` (3 lines, 34,833 bytes) |

**What this run is for.** The Safety net epic records one real run, and `dotnet test` replays the
whole app against it for free. This is that recording, made with the command in
`docs/runs/README.md`. It is the only paid run of the epic.

**The headline: it replays.** `ReplayTests` passes against this recording in 8 seconds with no
network, and the full suite passes 10 of 10. Each change the epic names fails it, at the line that
changed:

| Change | Result |
| --- | --- |
| `soften` → `weaken` in `SKILL.md` | Model call 1 differs at line 11: the sentence, recorded and now |
| `options.Tools = []` in `AgentLoop` | Model call 1 differs at line 2: `tool: lookup_archetype` against `--- system` |
| `history.AddRange(response.Messages)` left out | Model call 2 differs at line 52: `--- assistant` against `--- tool` |
| `NPCFORGE_SEED` not passed to the server | The seed test fails: `to keep a secret buried` against `to get out of an unpleas…` |

**Observed.**

- **The model called the tool.** Turn 1 asked for `lookup_archetype`, so the recording has two
  calls and the replay has a second request on which a dropped model turn shows. On 2026-09-20 (run
  05) one `luna` model never called it; the test now refuses a recording like that.
- **No reasoning item came back.** The reply's usage says `reasoningTokenCount: 0`, and neither
  message holds reasoning content. The round trip of reasoning content is proven only with a scripted
  reply, so a re-record that does reason is the first real test of it.
- **`UsageDetails` names cached and reasoning tokens itself** in Microsoft.Extensions.AI 10.10
  (`cachedInputTokenCount`, `reasoningTokenCount` in the transcript), so no `[loop]` extra-count lines
  were printed. The comment in `AgentLoop.cs` that says the cached count arrives only in
  `AdditionalCounts` is out of date.
- **`conversationId` equals `responseId`** on both replies. The app never passes it back, as
  docs/architecture.md says it must not.
- **`[server] dice: seed 1` comes at the first roll, not at server start.** The dice are made the
  first time `CharacterTools` is used.
- Mara Venn again. The obstacle rule holds at `Wont`: she knows the name and will not give it, and
  the weakness lever names what yielding costs her (involving herself risks the secret).
- The save went to the branch's copy of `characters.json` as 4 and was not kept. The recording is
  the exact record of the run, and the replay test checks its own save against it.

**How the console was captured.** Git Bash, `2>&1 | tee`: stdout and stderr through one pipe, in
the order the app wrote them, not a PowerShell redirect (lesson 005). The app now writes UTF-8, so
the curly quotes and dashes are as the model wrote them. There is no launch profile, so stdout has
no "Using launch settings" line above the answer.

## Console: the run (Git Bash, tee)

```text
[app] recording to C:\Users\camer\source\repos\NpcForge\.claude\worktrees\agent-a769199909f53e045\NpcForge.Tests\Fixtures\replay.jsonl
[server] info: ModelContextProtocol.Server.StdioServerTransport[857250842]
[server]       Server (stream) (NpcForge.Server) transport reading messages.
[server] info: Microsoft.Hosting.Lifetime[0]
[server]       Application started. Press Ctrl+C to shut down.
[server] info: Microsoft.Hosting.Lifetime[0]
[server]       Hosting environment: Production
[server] info: Microsoft.Hosting.Lifetime[0]
[server]       Content root path: C:\Users\camer\source\repos\NpcForge\.claude\worktrees\agent-a769199909f53e045
[server] info: ModelContextProtocol.Server.McpServer[570385771]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'server/discover' request handler called.
[server] info: ModelContextProtocol.Server.McpServer[1867955179]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'server/discover' request handler completed in 28.691ms.
[server] info: ModelContextProtocol.Server.McpServer[570385771]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/list' request handler called.
[server] info: ModelContextProtocol.Server.McpServer[1867955179]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/list' request handler completed in 11.6716ms.
[server] info: ModelContextProtocol.Server.McpServer[570385771]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler called.
[server] dice: seed 1
[server] info: ModelContextProtocol.Server.McpServer[2065726448]
[server]       "roll_character" completed. IsError = False.
[server] info: ModelContextProtocol.Server.McpServer[1867955179]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler completed in 61.4857ms.
[app] openai gpt-6-luna
[app] brief {"setting":"an inn in a major city","playersWant":"the name of a fence","difficulty":"Wall","occupation":"innkeeper","characterWants":"to keep a secret buried","obstacle":"Wont","attitude":"world-weary","mannerism":"tilts their head when listening","weakness":"cannot bear being left out","willNotDiscuss":"how they lost a tooth","wrongAbout":"thinks all adventurers know one another"}
[skill] C:\Users\camer\source\repos\NpcForge\.claude\worktrees\agent-a769199909f53e045\NpcForge.Console\bin\Debug\net10.0\skills\npc-writer\SKILL.md (2239 chars, 6b58d7c2)
[loop] turn 1: 1 tool call(s), history 3, 708 in / 21 out
[tool] lookup_archetype {"occupation":"innkeeper"}
[server] info: ModelContextProtocol.Server.McpServer[570385771]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler called.
[server] info: ModelContextProtocol.Server.McpServer[2065726448]
[server]       "lookup_archetype" completed. IsError = False.
[server] info: ModelContextProtocol.Server.McpServer[1867955179]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler completed in 0.9563ms.
[loop] turn 2: 0 tool call(s), history 5, 757 in / 2061 out
## 1. Who they are
**Mara Venn** has kept a city inn through years of changing tenants.

## 2. Where they stand
Mara knows the players are looking for a fence and knows the name they want. She will not give it up: doing so could bring the secret she’s protecting into the open.

## 3. How they seem, and what is underneath
She meets questions with a world-weary look, tilting her head as she listens. Beneath that, she’s determined to keep the secret buried—and can’t bear to be left out of what’s going on.

## 4. Levers
- **(Weakness)** Let her in on what the players know and what they plan to do next. She may give them the name, at the cost of risking her own secret by involving herself. **“You’re going to go looking either way. Don’t leave me guessing—tell me what you know, and I’ll tell you who to find.”**
- **(CharacterWants)** Convince her that the secret will stay buried if she helps. A promise alone may not be enough; she needs to believe the players understand what’s at stake. **“If I give you that name, you keep this out of my house. And out of anyone else’s mouth.”**
- **(WrongAbout)** Mention another adventurer the players know. Mara assumes adventurers all know one another, and may be drawn into comparing connections. **“You’ve crossed paths with the others, then. Tell me who you’ve spoken to, and I’ll tell you who you’re after.”**

## 5. Anti-levers
- **(CharacterWants)** Threaten to expose the secret or make a scene in the inn. Mara clamps down rather than risk it coming out. **“No. You’re not dragging that through my door.”**
- **(WillNotDiscuss)** Ask how she lost her tooth, or press her when she refuses to answer. She shuts the conversation down. **“That’s not your business. We’re done.”**
- **(WrongAbout)** Mock her assumption that all adventurers know one another. She digs in instead of admitting she might be wrong. **“You expect me to believe you don’t know a soul in your own line of work?”**

## 6. Afterwards
If the players earn her help, Mara gives them the name but watches closely to see whether they keep her secret buried. If they fail, she keeps quiet and remembers the intrusion. If they threaten her or expose the secret, she makes an enemy of them.
[server] info: ModelContextProtocol.Server.McpServer[570385771]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler called.
[server] info: ModelContextProtocol.Server.McpServer[2065726448]
[server]       "save_character" completed. IsError = False.
[server] info: ModelContextProtocol.Server.McpServer[1867955179]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler completed in 31.6897ms.
[app] saved as 4
```
