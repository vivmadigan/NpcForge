# Run 01 — 2026-09-22 — saved, then loaded back

| | |
| --- | --- |
| Model | openai / `gpt-6-luna` |
| Setting | an inn in a major city |
| Players want | the name of a fence |
| Difficulty | `Wall` |
| Obstacle *(rolled)* | `ForAPrice` |
| Occupation | innkeeper |
| Skill | `skills/npc-writer/SKILL.md`, 2239 chars, `6b58d7c2` |
| Commit | `7058568`. The only change in the tree was `characters.json`, which is data, not code |
| Tokens | turn 1: 711 in / 21 out · turn 2: 760 in / 1063 out · the load: none |

**What this run is for.** Step 8's "Done when": one paid run, then `--load` of its number,
prints the same character. The run was saved as 3; `dotnet run --project NpcForge.Console --
--load 3` followed it.

**The headline: it comes back unchanged.** The `[app] brief` line is the same in both, character
for character, and so is the text. The load made one `tools/call` (`load_character`), with no
roll, no `[skill]` line and no `[loop]` line: the model was never asked.

**Observed.**

- **The two consoles only look different.** The run was in Visual Studio's debug console, which
  shows `-`, `'` and `"` where the model wrote `—`, `’` and `“ ”`. The load was in a PowerShell
  terminal, which shows the real characters. `characters.json` holds the real ones. The
  byte-for-byte proof is the free test `Loads_a_saved_character_back_unchanged`, not this
  comparison by eye.
- **A paste is not a byte-exact record either.** Checked by script against save 3 in
  `characters.json`: the load below matches it line for line, except that the model ended six
  lines with two spaces (Markdown's line break, before each italic quote), and the paste lost
  them. Invisible on screen, 12 characters in the file.
- The app's own `save_character` and `load_character` calls leave only `[server]` lines, no
  `[tool]` line, like the roll: `[tool]` is traced in `InvokeAsync`, the model's door.
- The per-obstacle rule holds at `ForAPrice` again: Mara names the price (take the angry
  supplier off her hands) and the weakness supports the bargain rather than replacing it.
- Mara Venn again. On `gpt-6-luna` today the names were Merrit Vale, Mara Venn and Mara Venn,
  the same `Mar-`/`Mer-` pull that step 7 found in the OpenAI family.

## Console: the run (Visual Studio, F5)

```text
[server] info: ModelContextProtocol.Server.StdioServerTransport[857250842]
[server]       Server (stream) (NpcForge.Server) transport reading messages.
[server] info: Microsoft.Hosting.Lifetime[0]
[server]       Application started. Press Ctrl+C to shut down.
[server] info: Microsoft.Hosting.Lifetime[0]
[server]       Hosting environment: Production
[server] info: Microsoft.Hosting.Lifetime[0]
[server]       Content root path: C:\Users\camer\source\repos\NpcForge\NpcForge.Console\bin\Debug\net10.0
[server] info: ModelContextProtocol.Server.McpServer[570385771]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'server/discover' request handler called.
[server] info: ModelContextProtocol.Server.McpServer[1867955179]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'server/discover' request handler completed in 19.5768ms.
[server] info: ModelContextProtocol.Server.McpServer[570385771]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/list' request handler called.
[server] info: ModelContextProtocol.Server.McpServer[1867955179]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/list' request handler completed in 9.5142ms.
[server] info: ModelContextProtocol.Server.McpServer[570385771]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler called.
[server] info: ModelContextProtocol.Server.McpServer[2065726448]
[server]       "roll_character" completed. IsError = False.
[server] info: ModelContextProtocol.Server.McpServer[1867955179]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler completed in 38.6047ms.
[app] openai gpt-6-luna
[app] brief {"setting":"an inn in a major city","playersWant":"the name of a fence","difficulty":"Wall","occupation":"innkeeper","characterWants":"to get out of an unpleasant responsibility","obstacle":"ForAPrice","attitude":"easily offended","mannerism":"tilts their head when listening","weakness":"cannot bear being left out","willNotDiscuss":"their terrible sense of direction","wrongAbout":"believes they have an excellent singing voice"}
[skill] C:\Users\camer\source\repos\NpcForge\NpcForge.Console\bin\Debug\net10.0\skills\npc-writer\SKILL.md (2239 chars, 6b58d7c2)
[loop] turn 1: 1 tool call(s), history 3, 711 in / 21 out
[tool] lookup_archetype {"occupation":"innkeeper"}
[server] info: ModelContextProtocol.Server.McpServer[570385771]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler called.
[server] info: ModelContextProtocol.Server.McpServer[2065726448]
[server]       "lookup_archetype" completed. IsError = False.
[server] info: ModelContextProtocol.Server.McpServer[1867955179]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler completed in 1.2779ms.
[loop] turn 2: 0 tool call(s), history 5, 760 in / 1063 out
### 1. Who they are
**Mara Venn** - She has kept a busy city inn for years and has learned to notice who comes and goes.

### 2. Where they stand
Mara knows the players want the name of a fence. She won't give it away: first, they must take an unpleasant responsibility off her hands.

### 3. How they seem, and what is underneath
She listens with her head tilted, then takes offense at the slightest hint that she's being dismissed. Underneath, she hates being left out. Given an opening, she'll demonstrate what she considers her excellent singing voice.

### 4. Levers
- **ForAPrice - characterWants:** Take over the obligation Mara is trying to avoid: explain to a furious supplier why the inn's latest payment is late. If the players agree to handle it, she'll give them the fence's name.
  *"You want a name? Fine. You take the supplier off my hands, and I'll tell you."*
- **Weakness - cannot bear being left out:** Let her feel included in the plan, rather than asking her to hand over information and be forgotten. This supports the bargain; it doesn't replace it.
  *"You'll be in on it, of course. I wouldn't leave you out."*
- **Mannerism - tilts their head when listening:** Hear her out without interrupting; she's more willing to bargain when she feels listened to.
  *"Go on, then. I'm listening."*

### 5. Anti-levers
- **Attitude - easily offended:** Imply she's a coward or a fool for wanting out of the responsibility.
  *"Careful. I won't have you insulting me in my own inn."*
- **WrongAbout - believes they have an excellent singing voice:** Mock her singing or tell her she can't carry a tune.
  *"If that's your opinion, you can find your fence somewhere else."*
- **Weakness - cannot bear being left out:** Make it clear the players will exclude her once they get what they want.
  *"So you'll use me for the name and shut me out afterward? Then no."*

### 6. Afterwards
If the players take over the supplier conversation, Mara gives them the fence's name and is relieved to be free of the unpleasant duty. If they refuse, she keeps the name to herself. If they insult her or make her feel deliberately excluded, she remembers the slight-and may make sure the inn's regulars hear about it.
[server] info: ModelContextProtocol.Server.McpServer[570385771]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler called.
[server] info: ModelContextProtocol.Server.McpServer[2065726448]
[server]       "save_character" completed. IsError = False.
[server] info: ModelContextProtocol.Server.McpServer[1867955179]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler completed in 35.3576ms.
[app] saved as 3
```

## Console: the load (PowerShell terminal)

```text
PS C:\Users\camer\source\repos\NpcForge> dotnet run --project NpcForge.Console -- --load 3
[server] info: ModelContextProtocol.Server.StdioServerTransport[857250842]
[server]       Server (stream) (NpcForge.Server) transport reading messages.
[server] info: Microsoft.Hosting.Lifetime[0]
[server]       Application started. Press Ctrl+C to shut down.
[server] info: Microsoft.Hosting.Lifetime[0]
[server]       Hosting environment: Production
[server] info: Microsoft.Hosting.Lifetime[0]
[server]       Content root path: C:\Users\camer\source\repos\NpcForge
[server] info: ModelContextProtocol.Server.McpServer[570385771]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'server/discover' request handler called.
[server] info: ModelContextProtocol.Server.McpServer[1867955179]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'server/discover' request handler completed in 22.2899ms.
[server] info: ModelContextProtocol.Server.McpServer[570385771]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/list' request handler called.
[server] info: ModelContextProtocol.Server.McpServer[1867955179]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/list' request handler completed in 10.1704ms.
[server] info: ModelContextProtocol.Server.McpServer[570385771]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler called.
[server] info: ModelContextProtocol.Server.McpServer[2065726448]
[server]       "load_character" completed. IsError = False.
[server] info: ModelContextProtocol.Server.McpServer[1867955179]
[server]       Server (NpcForge.Server 1.0.0.0), Client (NpcForge.Console 1.0.0.0) method 'tools/call' request handler completed in 46.7453ms.
[app] loaded 3
[app] brief {"setting":"an inn in a major city","playersWant":"the name of a fence","difficulty":"Wall","occupation":"innkeeper","characterWants":"to get out of an unpleasant responsibility","obstacle":"ForAPrice","attitude":"easily offended","mannerism":"tilts their head when listening","weakness":"cannot bear being left out","willNotDiscuss":"their terrible sense of direction","wrongAbout":"believes they have an excellent singing voice"}
### 1. Who they are
**Mara Venn** — She has kept a busy city inn for years and has learned to notice who comes and goes.

### 2. Where they stand
Mara knows the players want the name of a fence. She won’t give it away: first, they must take an unpleasant responsibility off her hands.

### 3. How they seem, and what is underneath
She listens with her head tilted, then takes offense at the slightest hint that she’s being dismissed. Underneath, she hates being left out. Given an opening, she’ll demonstrate what she considers her excellent singing voice.

### 4. Levers
- **ForAPrice — characterWants:** Take over the obligation Mara is trying to avoid: explain to a furious supplier why the inn’s latest payment is late. If the players agree to handle it, she’ll give them the fence’s name.
  *“You want a name? Fine. You take the supplier off my hands, and I’ll tell you.”*
- **Weakness — cannot bear being left out:** Let her feel included in the plan, rather than asking her to hand over information and be forgotten. This supports the bargain; it doesn’t replace it.
  *“You’ll be in on it, of course. I wouldn’t leave you out.”*
- **Mannerism — tilts their head when listening:** Hear her out without interrupting; she’s more willing to bargain when she feels listened to.
  *“Go on, then. I’m listening.”*

### 5. Anti-levers
- **Attitude — easily offended:** Imply she’s a coward or a fool for wanting out of the responsibility.
  *“Careful. I won’t have you insulting me in my own inn.”*
- **WrongAbout — believes they have an excellent singing voice:** Mock her singing or tell her she can’t carry a tune.
  *“If that’s your opinion, you can find your fence somewhere else.”*
- **Weakness — cannot bear being left out:** Make it clear the players will exclude her once they get what they want.
  *“So you’ll use me for the name and shut me out afterward? Then no.”*

### 6. Afterwards
If the players take over the supplier conversation, Mara gives them the fence’s name and is relieved to be free of the unpleasant duty. If they refuse, she keeps the name to herself. If they insult her or make her feel deliberately excluded, she remembers the slight—and may make sure the inn’s regulars hear about it.
```
