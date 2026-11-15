# What the pages need from an API

The input to epic 6. Epic 12 built the website's two pages on made-up data behind one seam,
`NpcForge.Web/src/api.ts`: six async functions and their types (ADR-017, decision 3). Epic 6
replaces the bodies of those functions with calls to `NpcForge.Api` and keeps the signatures, so a
page changes only where the real API forces it.

This file lists, for each function, which page calls it, from which state, and what the page does
with the answer. The types are in `api.ts` and are not repeated here. A state name such as
`forge-confirm` opens that state at `/?state=forge-confirm`, and its screenshot is in
`docs/design/epic-12/`.

## The six functions

### `startInterview(scene)`

- **Called by** the forge page at `/`, when the GM sends the scene. The scene box is the first step
  (`forge-describe`), and it comes back after the brief is turned down (`forge-turned-down`) or a
  character is left (`forge-left`).
- **While it waits** the page shows `forge-reading`, a still screen.
- **The page needs** one of three answers:
  - a question for a missing place or role, with the field's name and the question's words
    (`forge-question`);
  - the confirmation: every brief field in the order to show, each with its label, and for a field
    with the GM's words the value, the quote and the sentence the quote is in (`forge-confirm`);
  - the spending cap, with the cap's message (`forge-cap`).
- **The page also needs** an interview id, which it passes back and never reads.

### `answer(interview, text)`

- **Called by** the forge page from `forge-question`, with what the GM typed.
- **The page needs** the same three answers as `startInterview`: the next question, the
  confirmation or the cap.

### `rollAndWrite(interview)`

- **Called by** the forge page from four places, each with the same interview id:
  - the yes on the confirmation (`forge-confirm`);
  - roll again on a written character (`forge-written`);
  - roll again after no name followed the dice (`forge-no-name`);
  - roll another after a save (`forge-saved`).
- **While it waits** the page shows `forge-writing`, a still screen with no progress.
- **The page needs** one of three answers:
  - a written character: a draft id, the joined name, the rolled brief in the order to show, and
    the sheet as Markdown (`forge-written`);
  - no name, with the reason in the console's words, such as
    `no name followed the dice (K, 2, V)` (`forge-no-name`);
  - the spending cap (`forge-cap`).

### `save(draft)`

- **Called by** the forge page from `forge-written`, with the draft on the screen.
- **The page needs** the number the character was saved as. It shows `forge-saved`, links to
  `/saved/<number>`, and asks for the count again.

### `listSaves()`

- **Called by** the saved page at `/saved` for the list: each save's number, joined name or none,
  occupation and setting, in number order (`saved-long`, `saved-empty`).
- **Called by** every page, once when it loads and again after a save, for the count in the top
  bar. Only the length is used.

### `loadSave(number)`

- **Called by** the saved page at `/saved/<number>`.
- **The page needs** the save's joined name or none, its brief in the order to show, and its sheet
  (`saved-open`, `saved-open-unnamed`), or nothing when no save has that number (`saved-missing`).

## Not calls

Turning the brief down and leaving a character send nothing, as in the console. The page puts the
scene back in the box.

## What the build found, for epic 6 to decide

- **An interview outlives its first character.** Roll again, roll again after no name and roll
  another after a save all pass the first interview id. The API must keep an interview's answers
  for as long as the page can still ask for new dice.
- **Only the draft on the screen is ever saved.** After roll again, the page forgets the earlier
  draft.
- **A save can be sent twice.** The page has no state between the click on save and the answer, so
  a second click on a slow connection sends `save` again. Either the API treats a second save of
  one draft as the first, or the page gains a saving state.
- **The wait shows no progress.** `rollAndWrite` is the slow call: it rolls, then calls the model
  for a name and again for the sheet. The page says those three steps in still text. ADR-014 names
  a way for a run to report progress. If epic 6 uses it, this signature changes and the wait
  screen can mark the step.
- **The bar's count costs a list.** Every page load calls `listSaves` and uses its length. A
  count of its own would be cheaper.
- **The page draws no failure but the cap and the missing name.** A dropped connection, a request
  from someone not logged in, and a save the store refuses have no state on either page.
- **`no-name` carries a brief the page does not show.** The page says what happened and offers roll
  again.
- **The cap's message is shown as sent**, between "Stopped:" and "Nothing was saved." So the API
  sends the message alone.
- **A brief field's name is shown as sent**, such as `playersWant`, and a note such as "nothing
  recorded, so the dice pick it after your yes" is shown as sent. Plain words for either would come
  from the API (ADR-017, "Left to the owner").
- **Paths the server must answer with the app's `index.html`:** `/`, `/saved` and
  `/saved/<number>`.
- **The made-up parts to delete:** `src/madeUp/saves.ts` and `saves.json` stay only if the named
  states outlive epic 6 (ADR-017, "Left to the owner"). The `sessionStorage` saves, the one-second
  wait and the run of three characters in `api.ts` go.
