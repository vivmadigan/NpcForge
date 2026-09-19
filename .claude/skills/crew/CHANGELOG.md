# Crew changelog

Every change to the crew's agents, skills and hooks, and to the instructions around them (the team rule, the playbook, AGENTS.md), with the reason and how to tell whether it helped. Newest first.

Each entry says what to watch for. If a change turns out bad, revert it and add a new entry saying why; do not edit or delete the old one.

## 2026-09-19 — retro after step 5 (NpcForge)

### step-done: facts go to the plan, not to /crew:lesson
- **File:** `skills/step-done/SKILL.md`, stage 3
- **Change:** a spotted item with a dead end goes to `/crew:lesson`. A plain fact becomes a note under the step's "worth remembering" notes.
- **Why:** the reviewer put a plain fact (the server `.exe` copied into `bin`) under "Lessons spotted". Stage 3 said to file it with `/crew:lesson`, and the lesson skill refuses anything without a dead end. The lead had to choose between the two rules.
- **Judge it by:** at the next step-done, spotted facts land in the plan notes without a judgement call, and the inbox only gets real lessons.

### reviewer: read untracked files
- **File:** `agents/reviewer.md`, procedure step 1
- **Change:** run `git status --untracked-files=all` and read new files in full. They never appear in `git diff`.
- **Why:** step 5 was mostly new files (a whole server project). The review covered them only because the lead listed them in the prompt.
- **Judge it by:** a plain "review it" mid-step, with no file list, still reviews the new files.

### librarian: fix links to moved inbox files
- **File:** `agents/librarian.md`, curate step 2
- **Change:** after moving or merging an inbox entry, point any links to its old inbox path at the new file.
- **Why:** `/crew:lesson` adds a pointer to the inbox path in the plan's step notes. On step 5 the librarian fixed it on its own initiative; nothing told it to.
- **Judge it by:** after a curate, searching the repo for `lessons/inbox/` finds nothing outside `docs/lessons/INDEX.md`.

## 2026-09-19 — during step 5 (NpcForge)

### team rule: throwaway code proves one claim only
- **File:** `.claude/rules/team.md` (project), "Who writes the code"
- **Change:** agents may build throwaway code outside the repo to prove one claim, never the whole proposal, and never show it or copy it in.
- **Why:** the first challenger run built all of step 5 in the scratchpad: 84k tokens, 33 tool calls, 6.5 minutes. Its findings were excellent; the full build was not needed to reach them.
- **Judge it by:** the next challenger run on a sketch costs well under that. If it still passes about 40k tokens, lower its `maxTurns` (25).

### playbook: the workflow as one diagram, and who the lead is
- **File:** `PLAYBOOK.md`, "The shape of it" and "A normal session"
- **Change:** the whole step as one diagram (sketch → challenger → type → test-runner → debugger → step-done → commit). The user is the supervisor; the main conversation is the lead. That matches how team.md and the agents already used "lead".
- **Judge it by:** at step 6, the diagram still matches what actually happens.

### lead instructions (AGENTS.md, project)
- **Sketches start with their usings,** and name any package the project does not reference yet. Why: step 5 sketches were pasted without them, and the console app had no MCP package. Judge by: pasting a sketch leaves only the `...` as errors.
- **Breakpoints after every build:** once new code builds, the lead suggests where to break and what to expect at each one. Why: the user asked for it after the step 5 run. Judge by: every step has a debugger pass.

## Known, not changed
- `reviewer` and `librarian` have `memory: project`, which gives them Write and Edit. So "read-only" for the reviewer is an instruction, not a wall. On step 5 it wrote only to `.claude/agent-memory/crew-reviewer/`. Revisit if it ever writes anywhere else.
- Reviewer cost on step 5: 86k tokens, 44 tool calls, about 4.5 minutes. Watch step 6 before cutting anything.
- The protect-docs hook and the Stop hook were never triggered on step 5. Untested until they fire, or until PLAYBOOK "First session" steps 2 and 6 are run.
