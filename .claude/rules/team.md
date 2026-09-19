# Team rule

This file is the one place where the crew's generic agents learn about THIS project. Every crew agent reads it first. Keep it short and factual; everything else about how the project works belongs in the documents it points to.

## Commands
- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project NpcForge.Console` (OpenAI by default; `-- --provider anthropic --model claude-sonnet-5` for Claude). Runs cost money; agents do not run the app unless asked.

## Where things are
- Plan with the step list and "Done when" conditions: `PLAN.md` (numbered steps, ✅ marks a finished one, each finished step has "Changed" and "Seen in the debugger, worth remembering" sections)
- What each step proves and the stop condition in one line: `BUILD.md` "Build steps"
- Documents that define how code here should read: `AGENTS.md` "Code" and "Easy to get wrong"; `BUILD.md` "How the code should read" and "Things that will bite"
- Decisions register (the live list, one line each): the "Decisions already made" table in `PLAN.md`. Full records with alternatives and consequences: `docs/decisions/` (immutable; a new record supersedes an old one). The table links to the record when one exists.
- Plan versions: `docs/plans/` (immutable snapshots; `PLAN.md` itself is live and edited freely)
- Lessons: `docs/lessons/` (curated) and `docs/lessons/inbox/` (drop new ones here). `docs/lessons/INDEX.md` is imported into every session.

## Who writes the code
The user writes the code. Agents explain, sketch, and review. Agents may build throwaway code outside the repo to prove one claim, never the whole proposal. It is never shown to the user or copied in. See `AGENTS.md` "What this project is for": a full implementation handed over is worth nothing here. Day-to-day advice ("one idea, a line on why") stays in the main conversation; `crew:reviewer` is for when the user asks for a review or a step is being finished, not for every edit.

## What "done" means for a step
The step's "Done when" line in `PLAN.md` (or `BUILD.md` "Build steps") holds, shown by evidence; `crew:reviewer` returns PASS or PASS WITH WARNINGS; anything learned in the step is in `docs/lessons/inbox/` or under the step's "worth remembering" notes; the step is marked ✅ with the date in `PLAN.md`. Some steps' conditions need a paid run or the user's eyes (three different innkeepers, levers that differ). The gate checks what it can and lists what the user must confirm; the user's confirmation closes it.

## When to delegate
- User asks for a review, or a step is being finished -> `crew:reviewer`
- A sketch or approach the user is about to build on, a decision, or a contested Critical finding -> `crew:challenger` before accepting it. Not for trivial choices.
- Claiming a step is done -> `crew:step-gate`, then the `/crew:step-done` skill
- Tests or a build need running -> `crew:test-runner`
- Inbox has entries, a decision was made or reversed, the user says "new version" -> `crew:librarian`

## Loops
The user writes the code, so the retry loop for code is the user, by design: sketch, the user types it, test-runner reports, the user fixes. For work the lead may iterate on itself (a test, a sketch, a doc), use `/goal <condition>` so a separate model judges when it is met, for example `/goal dotnet test passes with zero failures, or stop after 6 turns`. Always include a stop clause.

## Lessons
Lessons in this project are usually found by the user in the debugger and reported in chat. When the user reports one, or a turn solves a non-obvious problem, record it before finishing: `/crew:lesson <one line>` writes it to the inbox, or add a line under the current step's "worth remembering" notes in `PLAN.md`. End the turn with a line saying where it went ("Lesson recorded: docs/lessons/inbox/..." or "Noted under step 5"). A Stop hook reads that final line and will ask for it if a lesson was described but not recorded.
