# Team rule

This file is the one place where the crew's generic agents learn about THIS project. Every crew agent reads it first. Keep it short and factual; everything else about how the project works belongs in the documents it points to.

## Commands
- Build: `<command>`
- Test: `<command>`
- Run: `<command>`

## Where things are
- Plan with the step list and "done when" conditions: `<path>`
- Documents that define how code here should read: `<path>`, `<path>`
- Decisions: `docs/decisions/` (immutable; new record to change one)
- Plan versions: `docs/plans/` (immutable snapshots; the plan file itself is live)
- Lessons: `docs/lessons/` (curated) and `docs/lessons/inbox/` (drop new ones here)
- Path-scoped lesson rules: `.claude/rules/lessons-*.md`

## Who writes the code
<"The user writes the code; agents explain, sketch and review" | "Agents implement from an approved plan" | ...>

## What "done" means for a step
<e.g. "The step's 'Done when' line in the plan holds, shown by evidence; the reviewer's verdict is PASS or PASS WITH WARNINGS; lessons from the step are in the inbox.">

## When to delegate
- Code written or changed -> `crew:reviewer`
- A design choice, a sketch, or a Critical finding that matters -> `crew:challenger` before accepting it
- Claiming a step is done -> `crew:step-gate` first, then the `/crew:step-done` skill
- Tests or a build need running -> `crew:test-runner`
- Inbox has entries, a decision was made or reversed, a step finished -> `crew:librarian`

## Lessons
When a turn solves a non-obvious problem, record it before finishing: `/crew:lesson <one line>` writes it to the inbox. A Stop hook checks for this and will ask for it if it is missing.
