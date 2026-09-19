---
description: The ritual for finishing a plan step. Runs the gate, the review, and the librarian in order, and refuses to tick the step unless the evidence holds or the user confirms what only they can see. Use when the user says a step is done, or asks to tick it off, or is about to commit "step N".
---

Finish the step named in: $ARGUMENTS (if empty, the first step in the plan not marked done).

Run these in order. Each stage can stop the ritual. Do not skip a stage because the previous one "looked fine".

## 1. Gate
Delegate to `crew:step-gate` with the step number. Wait for its report.
- FAIL: show the user the "gap" list and stop. Do not tick anything. If the gap is something an agent may fix under the team rule (a test, a doc, not the user's code), offer to fix it and re-run the gate, at most twice.
- NEEDS HUMAN: show the user exactly which claims are theirs to confirm and what they need to observe. Wait for their answer. Their confirmation closes those claims; then continue.
- PASS: continue.

## 2. Review
Delegate to `crew:reviewer` for the files this step touched. Wait for its report.
- FAIL: show the Critical findings and stop.
- PASS or PASS WITH WARNINGS: continue. Carry the warnings and "Lessons spotted" forward.

## 3. Lessons
For each item under the reviewer's "Lessons spotted", and for anything in this conversation that qualifies, decide which kind it is. Something that failed first and was then understood has a dead end: run `/crew:lesson` so it lands in `docs/lessons/inbox/`. A plain fact with no dead end is not a lesson: add it as a note under the step's "worth remembering" notes in the plan instead. If there is at least one entry in the inbox, delegate to `crew:librarian` with the task `curate`. If the inbox is empty, skip the librarian.

## 4. Plan
Only now update the plan file: mark the step done with today's date, and add the step's notes in the plan's own style (look at how earlier finished steps are written and match it). This is the living document; edit it directly.

## 5. Decisions and versions
Two questions, with concrete triggers. Do not snapshot on a hunch.
- Was a decision made or reversed during this step, one that a future reader would want the alternatives and reasons for? If yes, delegate to `crew:librarian` with task `adr` and the decision, alternatives and reason. Trivial choices go in the plan's register as one line and get no record.
- Did any of these happen: the user said "new version"; a step's approach in the plan was rewritten rather than ticked; a decision in the register was reversed? If yes, delegate to `crew:librarian` with task `snapshot` and a one-line reason. Otherwise no snapshot. Ticking a step is not a new version.

## 6. Report
Tell the user, in this order: verdicts (gate, review), what was recorded (lessons, records, plan version), warnings carried forward, and a suggested commit message in the project's existing style (check `git log --oneline -5`). Do not commit unless asked. Suggest `git tag step-N` as a zero-cost marker of where the plan stood.
