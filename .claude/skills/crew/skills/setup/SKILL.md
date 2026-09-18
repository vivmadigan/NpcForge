---
description: Bootstrap the crew in a repository that does not have it yet. Creates the docs folders, the indexes, the team rule from its template, and the CLAUDE.md import. Use once per new project, or when the user asks to "set up the crew here".
disable-model-invocation: true
---

Set up the crew's files in this repository. Ask before overwriting anything that exists.

## Steps

1. Look at what is already here: `CLAUDE.md`, `.claude/rules/`, `docs/`, any plan or build document, the build tool (csproj, package.json, pyproject, Makefile). Read the top of the main docs so the team rule can point at them accurately.
2. Create, if missing:
   - `docs/decisions/INDEX.md` with a heading and a one-line explanation that records are immutable and superseded, never edited.
   - `docs/plans/INDEX.md` with the same for plan versions.
   - `docs/lessons/INDEX.md` and `docs/lessons/inbox/.gitkeep`.
3. Write `.claude/rules/team.md` from `${CLAUDE_PLUGIN_ROOT}/templates/team.md`. Fill every placeholder from what you found in step 1. For "Who writes the code", ask the user; do not guess. It changes what the reviewer and the lead do.
4. Add `@docs/lessons/INDEX.md` to `CLAUDE.md` (create the file if it does not exist), so every session sees the one-line lesson index.
5. Offer, in one line each, and do only what the user says yes to:
   - a first plan version (`crew:librarian`, task `snapshot`, reason "baseline"), if the project has a plan
   - seeding `docs/decisions/` from any decisions already written down in the project's docs (`crew:librarian`, task `adr`, one per decision)
   - seeding lessons from any "gotchas", "things that will bite", or "worth remembering" sections already in the docs
6. Tell the user the three commands they now have (`/crew:lesson`, `/crew:step-done`, `/crew:adr`) and the two hooks that are now active (immutable decisions, lesson check on Stop), and where to turn the Stop hook off if it gets in the way (`hooks/hooks.json` in the plugin folder). Say plainly that the template has been filled for one project so far and that the team rule is the file to fix if an agent does something odd here.
