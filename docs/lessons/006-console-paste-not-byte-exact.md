# A run and its --load look different in a console paste when the save is exact

- **Status:** active
- **Date:** 2026-09-22
- **Plan version:** v01 (see docs/plans/INDEX.md)
- **Applies to:** `docs/runs/**`, `NpcForge.Console/Program.cs`, `NpcForge.Server/characters.json` · tags: runs, console, encoding

## Symptom
Step 8's paid run (saved as 3) and its `--load 3`, pasted side by side, did not match. The run, in
Visual Studio's debug console, showed `**Mara Venn** - She`, `won't`, `*"You want a name?`. The
load, in a PowerShell terminal, showed `**Mara Venn** — She`, `won’t`, `*“You want a name?`.
A script comparing the recorded load with save 3 in `characters.json` then said
`identical: False`, `saved length: 2227`, `printed length: 2215`.

## What did not work
- Comparing the two consoles by eye. The Visual Studio debug console shows `—` `’` `“ ”` as
  `-` `'` `"`; the terminal shows them as written. Same text, different screens.
- Treating the paste as the record. Even the terminal paste is 12 characters short: the model
  ended six lines with two spaces (Markdown's line break, before each italic quote), and the
  paste lost them. Invisible on screen, so it reads as the save having changed the text.

## What worked
Compare the `[app] brief` lines (identical), then diff the printed text line by line against
`characters.json` by script: 27 lines each, and the only differences were the trailing spaces.
Byte equality itself is proved by the free test `Loads_a_saved_character_back_unchanged`, which
round-trips curly quotes, an em dash and line breaks. `Console.OutputEncoding = Encoding.UTF8`
at the top of `Program.cs` would make the debug console match; not applied yet.

## Why it works here, specifically
`docs/runs/` records runs by pasting the console (lesson 005 rules out redirecting), and step 8's
"Done when" compares a run with its load. The model writes typographic punctuation and Markdown
hard breaks, and both are exactly what a console or a clipboard quietly normalises. The file on
disk is the only byte-exact record.

## How to tell it is happening again
A run and its `--load` that differ only in `-`/`—`, `'`/`’`, or line lengths off by two.
