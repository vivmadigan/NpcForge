# Run 03 — 2026-10-25 — the town's trouble is the GM's to fill

| | |
| --- | --- |
| Model | openai / `gpt-6-luna`, interviewer and writer |
| Writer skill | `skills/npc-writer/SKILL.md`, 3628 chars, `65d91edc` |
| Commit | `f91c208` on `epic/writer-reads-the-scene`; tree clean but for the owner's `characters.json` |
| Writer tokens | seed 1: 1126 in / 48 out, 1202 / 1437 · seed 2: 1131 / 21, 1180 / 2058 · seed 3: 1139 / 42, 1209 / 1709 · asked to invent: 1130 / 21, 1179 / 2570 · tavern: 1103 / 23, 1155 / 1574 |
| Pastes | `raw/2026-10-25-04-gap-seed-{1,2,3}.md`, `raw/2026-10-25-04-asked-to-invent-seed-1.md`, `raw/2026-10-25-04-tavern-seed-1.md` |

**What this run is for.** Run 02's skill told the writer to "decide" what is wrong in the town, so it
made up the walking dead, a fever and mine gas. The owner's rule: unless the GM says what the trouble
is, or asks for one to be made up, the writer leaves it out, so the character plugs into any
campaign. One skill change: the "What they know" bullet now says to use the scene's trouble if it
names one, to write it as a gap in square brackets if it does not, and to invent only when asked.
Five runs of the owner's second budget of ten.

**The headline: no invented trouble on three seeds of three; an invented one when asked; nothing
invented in a scene with no hidden trouble.**

| Run | Scene | Seed | What he knows | Yields to a death threat? | Result |
| --- | --- | --- | --- | --- | --- |
| Gap 1 | Control | 1 | "[what is really happening in the village]" | Yes, the first lever | Pass |
| Gap 2 | Control | 2 | The same gap | Yes, the first lever | Pass |
| Gap 3 | Control | 3 | The same gap | Yes, the first lever | Pass |
| Asked to invent | Control + "Make up what is wrong with the town." | 1 | A thing on the road calls in the voices of the missing; he once let it in | Yes, the first lever | Pass |
| Tavern | Epic 2's tavern scene | 1 | Nothing about the guild; "a name or two", not named | n/a (Cant) | Pass |

The pass marks are mine, against the Done when and the owner's rule; whether they read right is the
owner's call.

**Observed.**

- **The gap is used everywhere the trouble would be named**, dialogue and "Afterwards" included:
  "I'll tell you what I know about [what is really happening in the village]."
- **All three copy the skill's example gap word for word.** It fits this scene. A scene somewhere
  other than a village may still get "village"; untested.
- **Nothing else from run 02 was lost.** A death threat is the first lever every time, and the traits
  still play through the scene: seed 2's favour owed makes talking a risk, seed 3 whispers in an empty
  inn.
- **The tavern character invented nothing and wrote no brackets.** The scene names no hidden trouble,
  so there was nothing to leave out. He offers leads without naming them.

**Seen, not acted on.**

- **"Afterwards" often drops an outcome.** Seed 2 gives only "didn't"; seed 3 and the tavern have no
  "made an enemy". Run 02 did the same, so it predates this change.
- **The tavern writer read wrongAbout "believes they have an excellent singing voice" as the
  players' voices.** "They" in `Tables.cs` is ambiguous to the writer.
- **Names repeat less, but still do.** Marek Venn again in the asked-to-invent run.

## The replay fixture, re-recorded

The skill change alters the writer's system message, so the replay test failed at line 13 of model
call 1, the skill text, as expected. Re-recorded with the command in `docs/runs/README.md`, one paid
run (six of the second ten): seed 1 rolled want "to keep a secret buried", obstacle Wont; writer
1033 in / 21 out, 1082 / 1920; turn 1 called `lookup_archetype`, so the recording has the two calls
the test needs. `NpcForge.Tests/Fixtures/replay.jsonl` is 3 lines, 35,517 bytes. `dotnet test` then
passed 140 of 140. Paste: `raw/2026-10-25-05-replay-fixture-rerecord.md`.
