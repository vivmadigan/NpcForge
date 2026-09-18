---
description: Record a decision as a new, immutable decision record. Use when a design choice is made or reversed, when the user says "let's go with X", or when a review or challenge settles an open question.
---

Record the decision: $ARGUMENTS

## Steps

1. If the decision is not yet challenged and it is not trivial, delegate to `crew:challenger` first with the decision and its alternatives. Show the user the verdict. Only continue if the user still wants it recorded, or the verdict was HOLDS.
2. Gather what the record needs: the context, the options considered (at least two), the choice, the reason, the consequences. If any of these is missing from the conversation, ask the user in one message with all the gaps listed.
3. Delegate to `crew:librarian` with task `adr` and all of the above. If this reverses an earlier record, name it.
4. Relay the file path written. If the plan file has a decisions table or section, add one line there pointing at the ADR; the plan is the living document, the ADR is the history.
