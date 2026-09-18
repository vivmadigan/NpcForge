---
description: Record a hard-earned lesson right now, before it is lost. Use whenever a non-obvious problem was just solved: an error that took more than one attempt, an SDK or provider behaving unexpectedly, a wrong assumption corrected, a workaround that is specific to this project.
---

Write one lesson file to `docs/lessons/inbox/` from what just happened in this conversation.

The one-line summary, if given: $ARGUMENTS

## Steps

1. Read `${CLAUDE_PLUGIN_ROOT}/templates/lesson.md`. Use every section. The "What did not work" section is mandatory; if nothing failed first, this was not a lesson and you should say so instead of writing a thin file.
2. Name the file `docs/lessons/inbox/YYYY-MM-DD-<slug>.md` with today's date and a slug from the problem, not the fix.
3. Fill in **Applies to** with the real paths or globs the lesson concerns, and one to three tags. This is what lets the librarian route it into a path-scoped rule.
4. Fill in **Plan version** from `docs/plans/INDEX.md` (the latest entry). If there is none, write `v00`.
5. Quote real error text and real file names. A lesson without the exact signature cannot be grepped for later.
6. Keep it under 40 lines. If it needs more, the problem is probably two lessons.
7. Add one line to `docs/lessons/INDEX.md` under the "Inbox (uncurated)" heading: `- [<title>](inbox/<file>) — applies to: <paths>`. The index is imported into every session, so this is what makes the lesson visible before the librarian has curated it.
8. If the project's plan file keeps per-step notes (a "worth remembering" or similar section under the current step), add a one-line pointer there too: `- Lesson: <title> (docs/lessons/inbox/<file>)`. Do not duplicate the content.

End with the line `Lesson recorded: <path>`, so the Stop hook can see it was recorded.
