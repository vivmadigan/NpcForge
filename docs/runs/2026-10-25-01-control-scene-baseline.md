# Run 01 — 2026-10-25 — the control scene, before epic 7

| | |
| --- | --- |
| Model | openai / `gpt-6-luna`, interviewer and writer |
| Scene | The control scene in ROADMAP.md epic 7, piped in with `yes` |
| Seeds | 1 and 2 |
| Interviewer skill | `skills/scene-interviewer/SKILL.md` sent as 3625 chars, `44b91186` |
| Writer skill | `skills/npc-writer/SKILL.md`, 2239 chars, `6b58d7c2` |
| Commit | `4d1ee13` on `epic/writer-reads-the-scene`; tree clean but for the owner's `characters.json` |
| Tokens, seed 1 | interview 1083 in / 498 out, 1693 / 45 · writer 707 / 21, 756 / 1262 |
| Tokens, seed 2 | interview 1083 / 297, 1453 / 111, 1586 / 17 · writer 712 / 21, 761 / 2049 |
| Pastes | `raw/2026-10-25-01-baseline-seed-1.md`, `raw/2026-10-25-01-baseline-seed-2.md` (server log lines left out) |

Run from Git Bash, saving to a scratch file through `NPCFORGE_CHARACTERS`:

```text
NPCFORGE_CHARACTERS=<scratch>/chars.json dotnet run --no-build -c Debug --project NpcForge.Console -- --seed <n> < control.txt > base-<n>.txt 2>&1
```

**What this run is for.** The baseline for epic 7: today's code, where the writer gets the brief and
never the scene. Two of the epic's ten paid runs.

**The headline: both fail the control.** Neither innkeeper knows what is going on in the town, and
neither yields to a threat on their life.

| Seed | Rolled want | Attitude | Knows what is off in the town? | Yields only to a death threat? |
| --- | --- | --- | --- | --- |
| 1 | to keep a secret buried | distrustful | No. "Knows the town's business"; the secret is unnamed and unconnected | No. Gossip and an expensive gift are the levers |
| 2 | to avoid someone they owe a favour to | easily offended | No. "Knows plenty about the town" | No. A small kindness or a promise of discretion |

**Observed.**

- **The interview did its part.** Both runs recorded setting, playersWant, occupation and obstacle
  Wont from the GM's words. Seed 1 inferred Wall from "unless threatened with death"; seed 2 left
  difficulty unrecorded, so it took the default, Wall. Either way the brief was right.
- **What the brief cannot carry is what was lost.** "Something is off about the town", "empty apart
  from the innkeeper" and "unless threatened with death" reach no field, so the writer never saw them.
  It wrote an ordinary inn.
- **The GM's "he" became "Mara Venn" in both runs**, and save 3 (a different brief) is also Mara
  Venn. The writer is told nothing about the character's sex, and its name comes from the model, not
  the dice. The scene fixes the first; repeating names are epic 3's.
- **Rolled traits are used as a checklist.** Every lever and anti-lever is one brief entry played on
  its own, as the skill's rule "every lever names the entry in the brief it comes from" asks.
