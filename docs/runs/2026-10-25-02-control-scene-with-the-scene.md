# Run 02 — 2026-10-25 — the control scene, with the scene sent to the writer

| | |
| --- | --- |
| Model | openai / `gpt-6-luna`, interviewer and writer |
| Scene | The control scene in ROADMAP.md epic 7, piped in with `yes`. `[app] scene sent (306 chars)` in every run |
| Interviewer skill | `skills/scene-interviewer/SKILL.md` sent as 3625 chars, `44b91186` |
| Commit, run A | `35e2400` (ADR-008 code, old writer skill `6b58d7c2`, 2239 chars), PR for `epic/writer-reads-the-scene` |
| Commit, runs B to D | `01bee36` (writer skill `c18284d9`, 3468 chars) |
| Writer tokens | A 802 in / 21 out, 851 / 1502 · B 1091 / 21, 1140 / 2575 · C 1095 / 21, 1144 / 3099 · D 1102 / 21, 1151 / 2560 |
| Pastes | `raw/2026-10-25-02-scene-old-skill-seed-1.md`, `raw/2026-10-25-03-scene-new-skill-seed-{1,2,3}.md` |

Run as in run 01, from Git Bash, saving to a scratch file. Runs 3 to 6 of epic 7's ten. Every
interview recorded difficulty Wall (inferred) and obstacle Wont, so the briefs differ only by seed.

**The headline: the scene alone was not enough; the scene and the skill together pass three seeds of
three.**

| Run | Seed | Skill | Rolled want, attitude | What he knows | Yields to a death threat? | Control |
| --- | --- | --- | --- | --- | --- | --- |
| A | 1 | old | keep a secret buried, distrustful | An unnamed secret | No: "Threats won't make him hand it over" | Fail |
| B | 1 | new | keep a secret buried, distrustful | The dead walk at night; the villagers hide in cellars under the inn | Yes, a credible one, and nothing else | Pass |
| C | 2 | new | avoid someone he owes a favour, easily offended | A fever from the old well; the healer he owes asked him to keep quiet | Yes, the first lever | Pass |
| D | 3 | new | keep a secret buried, trying hard to sound professional | A sealed mine vents deadly gas at dusk; the villagers shelter until the council moves them | Yes, "ordinary pressure won't do" | Pass |

The pass marks are mine, against the Done when; whether they read right is the owner's call.

**Observed.**

- **The scene arrived and was read (run A).** The character became "he", the inn "the village's only
  inn". The pronoun loss in run 01 is fixed by the scene alone.
- **The old skill turned the GM's condition upside down (run A).** It says every lever names "the
  entry in the brief it comes from", and the message says the brief wins. A death threat is in no
  brief entry, so it could not be a lever, and the writer wrote the opposite of the scene. This is
  the one-change-per-run rule paying off: the fault is the skill's rule, not the scene.
- **The new skill makes the scene settled too, says the character knows what the scene hints at,
  makes the GM's condition the first lever, and lets a lever cite the scene.** Runs B to D each name
  what is wrong in the town, make a death threat the way in, and play the rolled traits through it.
- **Light traits stay light, inside the situation.** Run D's innkeeper whispers in an empty inn and
  thinks nobody can hear him. Run C's petty "owes someone a favour" became a debt to the healer
  fighting the fever. Run B's distrust guards the hidden villagers.
- **The empty inn is explained every time.** All three use "empty apart from the innkeeper": the
  villagers are in hiding (B, D) or sick (C).

**Seen across the runs, not yet acted on.**

- **The town's trouble is the writer's choice**, so it can repeat across runs the way a model's
  trait does: two of three are "the villagers are hiding". A rolled or GM-given "what is wrong here"
  would put it back on the dice. An idea, untested.
- **Names repeat.** Tomas twice, and Venn in four of the six characters across runs 01 and 02
  (Mara Venn twice, Marek Venn, Tomas Venn). Epic 3's name dice.
- **`lookup_archetype` is called first in every run** and returns one fixed sentence, a turn that adds
  nothing to the character. An idea for epic 3's writers' lab, untested.
