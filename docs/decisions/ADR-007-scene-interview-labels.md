# ADR-007: The scene interview records the GM's own words, and only a choice may be inferred

- **Status:** accepted
- **Date:** 2026-10-25
- **Supersedes:** none

## Context
The "Talk to it" epic lets a GM start the app with no flags and describe a scene in one message. A
model reads the scene and works out the character brief. The app shows every field with a label and
the words behind it, and rolls only after the GM types yes. AGENTS.md's rule still holds: the model
never guesses. Every fact is told by the GM or rolled by the dice.

Three things in the code shaped the design:
- `roll_character` already takes every rolled trait as an optional parameter and rolls only the
  missing ones (since step 6). Nothing has called it that way yet.
- `roll_character`, `save_character` and `load_character` are app-only. The model never sees them.
- Of the brief's eleven fields, two have a fixed list of choices (`difficulty`, `obstacle`). The
  other nine are free text, and `roll_character` takes any string for them.

The epic first said an inferred answer is "kept and labelled, never rejected". Three independent
reviewers (Opus, Sonnet, Haiku) then showed the same hole. On a free-text field, the model could
record its own invention (mannerism "taps the bar nervously") with any real quote attached ("doesn't
want to get involved"). Every check passes, the dice are skipped, and a GM who says yes to eleven
plausible lines would rarely notice.

## Options considered
- **A** The model labels its own answers, and inferences are kept on every field, as the epic first
  said. The least code, but the label the GM trusts is the model's claim, and invention reaches the
  brief with a citation.
- **B** Code decides the label from the value and the quote, and inference is allowed on every field.
  Closes the false label, not the invention: an unrelated free-text value is still kept as inferred.
- **C** Code decides the label, and the field's kind decides what is allowed. A choice field may be
  inferred. A free-text field takes a span of the GM's own words, or it is not recorded and the dice
  roll it.
- **D** The interview runs on the server as a model-visible MCP tool. The server would need the GM's
  words and would hold conversation state, and the MCP contract would grow a stateful tool.

## Decision
C. In detail:
- **The tool.** The model sees one tool, `record_answer(field, value, quote)`, handled in the console.
  It has no label argument. It cannot reach `roll_character`, `save_character` or `load_character`.
- **The field list.** It comes from `roll_character`'s input schema in `tools/list`: names, order,
  choices, descriptions, and which fields are required. The one server change is a `[Description]` on
  each `roll_character` parameter. `roll_character` is app-only, so the tools the model sees do not
  change.
- **The checks,** each with its own rejection reason: an unknown field; an empty value; a choice not
  in the list; an empty quote; a quote longer than one sentence or 12 words; a quote not in the
  scene as whole words; a free-text value that is not a span of its quote. One normalising function
  (decided 2026-09-26, step 13 in `git show eddc444:ROADMAP.md`) is used on all of them.
- **The labels.**
  - *My words.* A free-text field. The value stored is the GM's text for that span, as typed.
  - *Inferred.* A choice field, such as difficulty Wall from "doesn't want to get involved".
  - *Rolled.* A trait nobody recorded. The dice pick it.
  - *Default.* A required field the scene never mentions (setting, want, difficulty, occupation)
    takes the default its flag has.
- **The confirmation** shows every field, its label, its quote and the whole sentence the quote came
  from. Anything but "yes", including end of input, stops without rolling. It is written to stderr,
  so stdout stays the character text.
- **The roll.** A described trait goes to `roll_character` through the parameter it already has.
  The difficulty filter shapes only the dice. A described trait that clashes with it (Easy with an
  obstacle of Wont) is shown labelled and kept.
- **Where labels stop.** Labels and quotes stay in the console. The writer gets the brief as today,
  and the save is still the brief and the text (ADR-003).
- **Flags.** A run with any answer flag, or with `--load`, skips the interview. `--record` with no
  answer flags is refused, because a recording's line 1 is flags.
- **Spending.** A token cap wraps the model client for the whole session, interview and writer
  together. It starts at 200,000 tokens.

## Why
The README's reason for the dice is that a model asked for a trait gives "one of about four answers,
forever". An inference on a free-text field is that same model choosing a trait, with a quote
attached to make it look told. A choice field is different. The model picks from a short fixed
list, and the GM can judge "Wall from 'doesn't want to get involved'" at a glance.

Code decides the label because only structured data can be checked (AGENTS.md: never parse the
model's prose). The cost is paraphrase. A trait the GM describes in other words ("reluctant" for
"doesn't want to get involved") cannot be recorded as the paraphrase. The GM's own span is used, or
the dice decide. Losing a paraphrase costs one roll. Keeping it would cost the rule the app is built
on.

Labels stay out of the save and the writer's message so that ADR-003 and the replay (ADR-005) do
not change. Epic 4 can carry them into the save when it needs them, with its own ADR.

## Consequences
- Easier: a GM describes a scene in their own words and every trait still traces to their words or
  the dice. Traits described by the GM finally reach the brief. A new trait on the server reaches
  the interview with no console change. The checks are pure functions with free tests.
- Harder: a paraphrased trait is lost to the dice, which can surprise a GM who described it clearly
  in other words. The interviewer skill has to teach the model to quote the GM's span exactly.
- The epic's earlier constraint, "Kept and labelled, never rejected", now applies to choice fields
  only. ROADMAP.md epic 2 is updated to match.
