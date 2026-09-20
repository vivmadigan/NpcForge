---
name: inbox-has-no-backlinks-yet
description: In this project, inbox lessons so far have had no other repo file pointing at their inbox path, so the "repoint links" step is often a no-op
metadata:
  type: project
---

The curate task says to search the repo for links to an inbox entry's path (usually a pointer in
the plan's step notes) and repoint them at the curated file. As of the 2026-09-20 curate, neither
inbox entry (tool-failed-prefix, powershell-redirect-reorders-trace) was linked from anywhere else
in the repo — not PLAN.md's "worth remembering" notes, not BUILD.md, not docs/runs/. The only
reference was the pointer line in `docs/lessons/INDEX.md` itself, which the curate step already
rewrites.

**Why:** `team.md`'s workflow files a lesson via `/crew:lesson`, which (per its own description)
writes to the inbox and, it seems, also adds the INDEX.md inbox pointer line at file time — it
does not appear to also cross-link from PLAN.md step notes at that point. The user's alternative
path, adding a line under a step's own "worth remembering" notes in PLAN.md, doesn't reference the
inbox file at all — it's a separate, self-contained note.

**How to apply:** still run the repo-wide grep for the inbox filename before deleting it (it is
cheap and the rule exists for when it does matter), but don't be surprised or over-search when it
comes back empty — one hit in INDEX.md is the expected/normal case here, not a sign something was
missed. See also [lesson-clustering](lesson-clustering.md).
