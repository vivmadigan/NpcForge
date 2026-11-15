# ADR-018: The website's confirmation is the scene and a few sentences, written by the page from what the scene settled

- **Status:** accepted
- **Date:** 2026-11-15
- **Amends, for the website only:** one line of ADR-007, one line of ADR-010 decision 4, and five
  parts of ADR-017. Each decision below names the line it changes. The console keeps its full
  confirmation, and no rule about how an answer is recorded changes.

## Context

Epic 13, "A forge in a cavern". The website's confirmation shows 19 rows, one for each brief field.
A row holds the field's name as the console prints it, such as `playersWant`, its label, and either
a quote with its sentence or a note. Twelve of the bakery run's 19 rows say only that the dice will
pick the field. The owner approved a sketch, `docs/design/cavern-sketch.html`. Its confirmation is
the GM's scene and a few sentences, with each value the GM gave in a second colour:

> The NPC we are building is a male goblin whose occupation is a baker. The setting is a tiny
> bakery, and he is currently furious. The tone you want is comic. You did not say what the players
> want from him, so he will be written around winning their goodwill.

The epic asks for a design check and an ADR before the confirmation is built. One question was
open: what crosses the seam (`NpcForge.Web/src/api.ts`) for the confirmation, and where its
sentences are made once epic 6 has an API. An answer has to cope with four cases: a want that was
not given, a want that was given, a scene with no gender, and a value the GM typed when asked.

Three constraints shape the answer. Epic 13 changes no file in `NpcForge.Core`, `NpcForge.Console`
or `NpcForge.Tests`. No model writes the sentences, and the page never cuts them out of prose. The
sketch settles the look. The owner chose among the options below on 2026-11-15.

## Options considered

- **Finished strings cross**, `sentences: string[]`, made in the core in epic 6. It is the smallest
  seam. The page could not colour the GM's values without searching a string, and a test could not
  tell a recorded value from the words around it.
- **Sentences cross as marked parts**, each part plain text or a value the GM gave, made in the core
  in epic 6. The core already knows the fields, their choices and which field can be "not given", so
  there is one list, and the console could use it. But epic 13 may not change the core, so its
  sentence builder would be TypeScript that epic 6 deletes and rewrites in C#. The wording the owner
  judges in epic 13 would be built twice.
- **The settled fields cross, and the page writes the sentences.** Chosen.

## Decision

1. **ADR-017 decision 3, the confirmation's shape at the seam.** One entry crosses for each field
   the scene settled. It replaces `Quoted`, `ConfirmField` and the `fields` of `InterviewStep`. The
   six functions keep their signatures. The page keeps the scene it sent, so the scene does not
   cross back.

   ```ts
   // The brief's field names, in the order the roll lists them.
   export type BriefField =
     | 'setting' | 'playersWant' | 'occupation' | 'difficulty' | 'characterWants' | 'obstacle'
     | 'attitude' | 'mannerism' | 'weakness' | 'willNotDiscuss' | 'wrongAbout' | 'tone' | 'race'
     | 'nameLetter' | 'nameSyllables' | 'familyLetter' | 'gender' | 'age' | 'look';

   // One field the scene settled, in the roll's order, with the value the brief will hold. null is
   // "not given" (ADR-010 decision 2). A field the dice will pick has no entry.
   export type ConfirmEntry = { field: BriefField; value: string | null };

   export type InterviewStep =
     | { kind: 'question'; interview: InterviewId; field: string; question: string }
     | { kind: 'confirm'; interview: InterviewId; entries: ConfirmEntry[] }
     | Stopped;

   export type RunResult =
     | { kind: 'written'; draft: DraftId; name: string; sheet: string }
     | { kind: 'no-name'; reason: string }
     | Stopped;

   export type SavedCharacter = { number: number; name: string | null; sheet: string };
   ```

2. **The page writes the sentences**, in epic 13 and in epic 6. A pure function in
   `NpcForge.Web/src/` takes the entries and returns each sentence as a list of parts, each plain
   text or a value. The confirmation draws the parts and colours the values, so it never searches a
   string. A test can import the function, as `states.spec.ts` imports the table of states.

   | The case | What crosses | What the page writes |
   | --- | --- | --- |
   | A want that was not given | `{ field: 'playersWant', value: null }` | That the GM did not say what the players want, so the character is written around winning their goodwill |
   | A want that was given | `{ field: 'playersWant', value: 'the fastest road to the capital' }` | A sentence that holds those words |
   | A scene with no gender | No `gender` entry | No gendered pronoun: he, him, his, she, her or hers |
   | A value typed when asked | An entry, as for a value read from the scene | The same sentence, with no mark |

   - A table keyed by `BriefField` gives each field its words, a phrase that the page joins into
     the sketch's few sentences. `tsc` fails when a field has none.
   - Free text appears exactly as sent. A choice appears in plain words from the same table, so
     `Comic` reads "comic" and `SomeWork` reads "some work". The epic's test of "every recorded
     value, ignoring case" reads a choice through those plain words.
   - The entries arrive in the roll's order. The order of the sentences is the page's own.

3. **In epic 6 the API sends the entries and nothing else for the confirmation.** For each line of
   `SceneDraft.Lines()` it sends the field's name as the core spells it. A line labelled my words or
   inferred carries its value as the core holds it, a choice as the roll spells it. A line labelled
   not given carries null. A rolled line, a label, a quote and a quote's sentence are not sent.
   `api.ts` parses the JSON into `ConfirmEntry` and throws on a name outside `BriefField`. It never
   drops an entry. The core gains no code for the sentences.

4. **ADR-007, "The confirmation shows every field, its label, its quote and the whole sentence the
   quote came from."** On the website it shows the scene and the sentences. It shows no label, so
   an inferred value reads the same as one the GM said. It shows no quote, no sentence the quote
   came from, and nothing for a field the dice will pick. The core still decides each label and
   keeps each quote, and the console still prints them.

5. **ADR-010 decision 4, "The confirmation shows the value, `[my words]`, and a line saying it was
   typed when asked."** On the website a typed answer is an entry like any other. The `[my words]`
   label and the "typed when asked" line both go. The `[not given]` label of ADR-010 becomes the
   sentence in decision 2's first row.

6. **ADR-017 decision 3, what the made-up interview answers.** `startInterview` still answers the
   bakery confirmation for any scene and never reads the scene. The answer is now seven entries, and
   it comes after a wait of one second, as `rollAndWrite`'s does, so the flame shows on send.
   `answer` gives the same entries with no wait. A second made-up confirmation holds three entries
   from `docs/runs/raw/2026-11-03-01-epic10-interview-nogender.md`: a place, a want and a role, with
   no gender. It opens only by its own named state (ADR-017 decision 6).

7. **ADR-017, "Left to the owner", how a brief field's name reads.** The default was the name as the
   console prints it. Now no words of the page's own show a joined name. The confirmation says each
   field in a sentence, and no page shows the brief. The one question still shows its `field`,
   which is `setting` or `occupation`, the only two fields that are asked for. The sheet is the
   model's Markdown and still names fields in its headings (epic 13, "Not in this epic"). ADR-017
   said plain words would need a second list of the fields. `BriefField` and its table are that
   list.

8. **ADR-017's default, "Nothing moves or makes a sound, so the wait is a still screen with no
   spinner."** The pages now move in two ways. Sending a scene and rolling a character fade to
   black, and nothing else fades. A flame burns during a wait, for as long as the seam's call takes,
   and the page adds no wait of its own. A device that asks for less motion gets a still flame and
   no fade. Nothing makes a sound. The screenshots and the contrast check run with the flame held
   still.

9. **ADR-017's default, "The rolled brief is shown beside the character"**, and decision 1's line
   for `Sheet.tsx`. No page shows the brief, on the forge page or on an opened save. The seam's
   types are what a page needs (ADR-017 decision 3), so `brief` leaves `RunResult` and
   `SavedCharacter`, and `BriefEntry` leaves `api.ts`. The made-up saves in `sessionStorage` keep
   their brief, because `listSaves` reads the occupation and the setting from it. If the owner
   decides that an opened save shows its brief, `brief` returns to `SavedCharacter`, and how a
   field's name reads is open again.

10. **ADR-017 decision 6, where the screenshots go.** `npm run screenshots` writes
    `docs/design/epic-13/<state>-<phone|desktop>.png`. `docs/design/epic-12/` stays as the record of
    the first look, and no command rewrites it.

## Why

- **The wording the owner judges in epic 13 is the wording that ships.** Epic 13 cannot change the
  core, so only a builder in the page is written once. Epic 6 then sends data the core already
  computes.
- **Only one front end needs the rule.** AGENTS.md puts a rule in the core when both front ends
  need it. The console keeps its 19 rows, so the sentences are one front end's way of showing the
  fields (principle-boundary-discipline: the seam carries what the scene settled, not how one page
  words it).
- **The entry is the brief's own shape.** A name and a value, where null already means "not given",
  so the four cases need no new idea (principle-laziness-protocol). The table keyed by the names
  turns a forgotten field into a compile error (principle-model-the-domain).

## Consequences

- **Two lists of the fields.** `Dice.Fields` in the core and `BriefField` in the page, whose table
  also holds plain words for each choice. A field or a choice added to the roll needs a line in the
  page. `tsc` holds the table to `BriefField`. Nothing holds `BriefField` to the core until epic 6,
  where `api.ts` throws on a name it does not know. Epic 6's design check decides whether a test
  holds the two lists equal.
- **The GM no longer sees which words a value came from**, or whether it was read or inferred. The
  console shows a quote's whole sentence so that a quote that drops a "doesn't" is visible. On the
  website that mistake shows only as a sentence that contradicts the scene above it.
- **Free text is the GM's words, so a sentence cannot assume how they start.** The sketch writes
  "a tiny bakery". The same sentence would write "a the Gilded Flagon". The build words each
  sentence with that in mind. *Needs the owner's eyes*, on both made-up confirmations.
- **If the owner decides the console shows sentences too**, both front ends need the rule. The
  builder then moves to the core, the seam carries marked parts, and a new ADR says so.
- **Epic 13's build follows this record.** It rewrites `docs/design/epic-12-api-needs.md` from the
  new `api.ts` and changes the screenshots line in AGENTS.md. Its first PR notes this record in the
  status lines of ADR-007, ADR-010 and ADR-017, which this PR may not touch.
