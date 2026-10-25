# A writer told "the brief wins" and "every lever names its brief entry" writes the opposite of the scene

- **Status:** active
- **Date:** 2026-10-25
- **Applies to:** `NpcForge.Console/skills/npc-writer/SKILL.md`, `NpcForge.Console/ChatAgent.cs` · tags: skill, scene, writer

## Symptom

Epic 7 sent the GM's scene to the writer (ADR-008). The control scene says the innkeeper "refuses to
give them anything, unless threatened with death". The first run with the scene sent (run A in
`docs/runs/2026-10-25-02-control-scene-with-the-scene.md`) read the scene: the character became "he"
and the inn a village inn. Then it wrote "Threats won't make him hand it over", and in "Afterwards",
"Threatening him makes an enemy; it still doesn't make him tell". The town's secret stayed unnamed.

## What did not work

Sending the scene on its own, with the old writer skill. The message said "where it and the brief
disagree, the brief wins", and the skill said "every lever and anti-lever names the entry in the
brief it comes from". A death threat is in no brief entry, so the writer could not make it a lever.
It treated the GM's condition as a clash with the brief and settled it for the brief, by ruling the
threat out.

## What worked

Telling the skill what the scene is for, in a section of its own: the scene is settled too; the
character knows what the scene hints at and says it plainly; what the scene says would make them
give way is the first lever and nothing in the brief overrides it; a brief entry wins only on a
clash about a trait. And one word in the rules: a lever names the brief entry "or the words of the
scene" it comes from. The next three runs, on three seeds, passed the control.

## Why it works here

The skill's rules are what the writer obeys first. "The brief wins" was meant for a trait the scene
seems to contradict, not for the scene's situation, and nothing said so. A rule that every lever
cites a brief entry also means no lever can come from anywhere else. Saying what the scene settles,
and letting a lever cite it, removes the clash the writer was resolving.

## How to tell it is happening again

A character written from a scene does the opposite of something the GM said, or leaves out the one
thing the scene stressed, while every rolled trait is played faithfully. Look for a rule in the skill
or the message that only the brief can satisfy. One change per paid run is what made this visible:
the scene and the skill went in as two runs, not one.
