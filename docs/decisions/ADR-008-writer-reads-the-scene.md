# ADR-008: The writer is sent the GM's scene, word for word, beside the brief

- **Status:** accepted
- **Date:** 2026-10-25
- **Supersedes:** ADR-007's line "The writer gets the brief as today". The rest of ADR-007 stands.

## Context
The scene interview (ADR-007) reads the GM's scene, records the fields it can quote, and drops the
scene. The writer is sent the skill and the brief, nothing else. A field holds a short span of the
GM's words, so everything the scene says that no field holds is lost: the mood, what the character
knows, what would make them yield.

Save 5 and the baseline runs (`docs/runs/2026-10-25-01-control-scene-baseline.md`) show the cost. The
control scene says "something is off about the town" and "he refuses to give them anything, unless
threatened with death". All three characters knew nothing particular about the town, none yielded
to a threat, and two of the three came back as a woman.

Epic 7 in ROADMAP.md asks for the scene to reach the writer, with flag runs and the replay fixture
unchanged.

## Options considered
- **A** Keep the scene out, and add fields until the brief can hold it (what they know, what makes
  them yield, a tone). Each field changes `tools/list` and needs the GM's words in a 12-word span. The
  mood of a scene is not a field.
- **B** Send the scene to the writer, word for word, in the writer's first message beside the brief.
  Code never reads it. Nothing is saved.
- **C** As B, and save the scene with the brief and the text. That changes `save_character`, its
  schema in `tools/list`, and the save format (ADR-003). A load never calls the writer again, so
  nothing reads a saved scene yet.

Three independent design runners (Opus, Sonnet, Haiku) each chose B without seeing the others.

## Decision
B. In detail:
- `SceneInterview.RunAsync` hands back `Told(Scene, RollArguments)` after a yes, and null otherwise.
  The scene is the line the GM typed, trimmed. A "no" passes nothing on.
- `ChatAgent.RunAsync` takes `string? scene`, with no default, so every caller says which it is.
  Program.cs passes the interview's scene, and null for a run with answer flags.
- The writer's first message is built by one pure function, `ChatAgent.FirstMessage(briefJson,
  scene)`. With no scene it returns today's message exactly. With a scene, the order is the
  instruction, the scene between `<scene>` tags, then the brief JSON last. The message says the brief
  wins where the two disagree.
- How the writer should use the scene is the npc-writer skill's job, not the message's.
- One stderr line, `[app] scene sent (<n> chars)`, shows in every run's record that the scene went.
- The scene is not saved.

## Why
The README's case for the dice is that a model asked to choose a trait gives "one of about four
answers, forever". The scene is not the model choosing anything. It is what the GM told it, which is
the other half of "the model never guesses: it is either told or it rolls". A writer who is told the
scene is not inventing the town's secret; it is reading it.

The brief still wins on a clash, so the dice keep their job. A rolled trait the scene seems to
contradict is played through the scene, not dropped. Code never reads the scene, so the rule "never
parse the model's prose" is untouched: the scene goes in as it was typed and is never inspected.

With no scene the message is byte for byte today's, so the replay fixture, a flag run, passes with no
re-record. Saving the scene waits until something reads it back: epic 4 ("Keep and edit") can carry
the scene and the labels into the save together, with its own ADR.

## Consequences
- Easier: everything the GM said reaches the writer, including what no field can hold. A new kind of
  detail in a scene needs no new field.
- Harder: the writer's input is now partly free text, so how a character turns out depends on the
  skill as much as the brief. The skill has to say how to use the scene. A saved character no longer
  holds everything the writer was given; the run record in `docs/runs/` does.
- The free tests prove the scene is sent, not that it is used well. Only paid runs show that.
- `ReplayTests` finds the brief at the message's first `{`. A future replay of a scene run needs it
  found from the end, as the scene may hold a brace.
- Revisit if: epic 4 needs the scene in the save, or a flag run should take a scene too.
