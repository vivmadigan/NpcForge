# Crew changelog

Every change to the crew's agents, skills and hooks, and to the instructions around them (the team rule, the playbook, AGENTS.md), with the reason and how to tell whether it helped. Newest first.

Each entry says what to watch for. If a change turns out bad, revert it and add a new entry saying why; do not edit or delete the old one.

## 2026-09-19 — during step 6 (NpcForge)

### test-runner: report errors, do not prescribe fixes
- **File:** `agents/test-runner.md`, rules
- **Change:** "never attempt a fix" becomes "never attempt or suggest a fix", with a line on why.
- **Why:** a build failed because a new `ListAsync` was pasted over the `tools/list` lines inside `ConnectAsync`. The runner added an "Issue" section telling the user to close `ConnectAsync` after the `_client` line. That builds, but it drops `_tools = await _client.ListToolsAsync(...)`, so the model would silently get no tools. The report format already said "nothing else"; that was not enough.
- **Judge it by:** the next failed build comes back as errors and file:line only, with no advice.

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
- Reviewer at step 6's step-done: 80k tokens, 32 tool calls, 3.6 minutes, close to step 5. It was given no file list and still reviewed the untracked `McpToolSourceTests.cs`, so the "read untracked files" tweak held. Not cut.
- Challenger on the whole step 6 plan: 70k tokens, 21 tool calls, 3.3 minutes. That is over the 40k line in "throwaway code proves one claim only". `maxTurns` was not lowered. The one throwaway (a server and client on MCP 2.2.0) was the evidence for the strongest objection: a failed roll reaching the model as the brief. Any cut deep enough to matter would have stopped the run before that. Judge the line again on a single sketch, not a whole step.
- The protect-docs hook and the Stop hook were never triggered on step 5. Untested until they fire, or until PLAYBOOK "First session" steps 2 and 6 are run.
- The 40k line, measured on a single proposal at last (2026-09-20, step 7), as the "throwaway code proves one claim only" entry asked. Challenger on the whole step 7 plan: 73k tokens, 29 tool calls, 6.4 min. Challenger on one proposal (the run-log idea): 57k, 17 calls, 3.2 min. So a single proposal is about three quarters of a whole step, not a quarter — the floor is the reading it does to get its bearings, not the proposal's size. Both runs earned it: the first found a carry-over the plan had dropped, the second found that the file the proposal depended on was untracked. `maxTurns` still not lowered; 40k looks like the wrong number rather than a missed target.
