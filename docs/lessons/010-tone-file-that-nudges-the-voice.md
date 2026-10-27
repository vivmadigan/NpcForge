# A tone file that only nudges the voice gives six characters that read the same

- **Status:** active
- **Date:** 2026-10-27
- **Applies to:** `NpcForge.Console/skills/npc-writer/tones/*.md`, `NpcForge.Console/skills/npc-writer/SKILL.md` · tags: skill, tone, lab

## Symptom

Epic 3's blind tone check (`--lab tone --repeat 2 --mode blind --seed 7`): six characters from one
brief, two of each tone. The owner read the sheet and could not score it: "they all sound serious".
The six had the same structure, the same levers and most of the same lines. Comic showed as a quip
or two, Heartfelt as an offered seat.

## What did not work

The first tone files (unit 7, `e39cee9`). Each was about 800 characters of advice on the voice:
"play it straight inside the joke", "one good laugh beats five forced ones", say the feeling
"plainly and once". They were placed after a 3,700-character skill whose rules pull everything
towards grave: the situation stays real, nobody hands things over for free, a Wall costs the
character something. Advice on the voice lost to rules about the content.

## What worked

Each tone file says what the tone changes in every one of the six output sections, not only the
voice: the line of history, the reason for their position, the surface, the levers, how the Wall
holds, and the afterwards. Each opens with the bar: a reader names the tone from the first two lines,
and this section decides how it sounds while the brief decides what is true. The same lab, same seed
and brief, scored 5 of 6 (`docs/runs/2026-10-27-01-epic3-checks.md`).

## Why it works here

The writer obeys the skill's structure first and fills each section from the brief. A tone that
names what to put in each section competes on the same ground as the rules; a tone that names only a
voice is applied as a light finish over content the rules already decided.

## How to tell it is happening again

A lab sheet where the variants differ only in a few words of dialogue. Before writing a new tone or
style file, check that it says what changes in each section of the output. Judge it blind on a
sheet, not by reading one character.
