# Crew playbook

The README says what each agent is. This says how to use them together. Read the README once; keep this one open.

## The shape of it

You are the supervisor. The main conversation is the lead: it plans and sketches, sends agents out with a narrow job, and reads their reports. Agents report to the lead; the lead reports to you. Nothing is ever delegated to an agent that the main conversation could do in two lines, and no agent ever writes your code.

| Situation | Reach for | Why this one |
| --- | --- | --- |
| "Is this approach right?" before you build on it | `crew:challenger` | Fresh context. It has not read the conversation that produced the idea, so it cannot be carried along by it. |
| "Review what I wrote" | `crew:reviewer` | Reviews against the project's own rules, cites lessons, spawns the challenger on judgement calls. |
| "Run the tests" | `crew:test-runner` | Cheap, fast, reports only failures. Keeps 200 lines of test output out of the main conversation. |
| "Are we done with this step?" | `crew:step-gate` | Quotes the plan's stop condition and demands evidence for each claim. Cannot be talked into PASS. |
| "Record this" (a decision, a lesson, a change of direction) | `crew:librarian` | The only agent that writes files. Curates rather than collects. |

Rule of thumb for the models: the reviewer and challenger are on the strongest model and cost accordingly. Use them at decision points and at the end of a step, not after every edit.

## A normal session

Most of a session is you and the main conversation, exactly as before. The crew changes four moments. The whole of a step, start to finish:

```text
"We're on step N"
        │
        ▼
Main conversation sketches ──▶ crew:challenger (in the background, while you read)
        │                         └─▶ HOLDS: go · WEAKENED: your call · REFUTED: sketch reworked
        │  wait for the verdict before you type
        ▼
You type it ──▶ "run the tests" ──▶ crew:test-runner ──▶ build errors and failing tests only
        ▲                                                     │
        └─────────────────────── you fix ◀────────────────────┘
        │ green                  optional: "review it" ──▶ crew:reviewer
        ▼
Run it, step through it. Surprised? Say what you saw
        ├─ there was a dead end ──▶ /crew:lesson ──▶ docs/lessons/inbox/
        └─ just a fact ───────────▶ a note under the step in PLAN.md
        │
        ▼
/crew:step-done N
  1 crew:step-gate   PASS: on · FAIL: stop, the gap is listed · NEEDS HUMAN: you confirm, then on
  2 crew:reviewer    a Critical: stop and fix · otherwise on, warnings carried to the report
  3 lessons          /crew:lesson for each one spotted · crew:librarian curates if the inbox has entries
  4 PLAN.md          the main conversation ticks step N with the date and writes its notes
  5 two questions    a decision worth a record? a new version? only on a yes: crew:librarian
  6 report           verdicts, what was recorded, warnings, a suggested commit message
        │
        ▼
You commit, then git tag step-N
```

The four moments, one at a time:

**1. Before you build on a sketch.** When the main conversation gives you a sketch or an approach for something non-trivial (a new class, a seam, a way of wiring two things), say "challenge that" before you type it. You get one strongest objection and a verdict. HOLDS means go. WEAKENED means read the objection and decide. REFUTED means the main conversation reworks it. This is the two-minds-not-one mechanism, and it costs one agent call.

Skip it for trivial choices. A variable name does not need a challenger.

**2. When you have written the code.** Say "run the tests" and you get a short failure report from the test runner. Fix, repeat. This is your loop; nothing automates it because you are the one learning. When the tests are green and you want a second pair of eyes, say "review it". The reviewer runs the diff against AGENTS.md and BUILD.md, checks the lessons index for repeats, and returns Critical / Warnings / Suggestions. Fix the Criticals. Warnings are your call.

**3. When something surprised you in the debugger.** Say what you saw. If it was a real problem with a dead end before the fix, run `/crew:lesson <one line>` and it lands in `docs/lessons/inbox/`. If it was just a fact worth remembering, it goes under the step's "worth remembering" notes in PLAN.md as it always has. The Stop hook reads the last line of the turn and nudges if a lesson was described but not recorded. Ending the turn with "Lesson recorded: ..." or "Noted under step 5" is what satisfies it.

**4. When you think a step is done.** Run `/crew:step-done 5`. It runs the gate, then the review, then files lessons, then ticks PLAN.md, then asks two concrete questions about decisions and direction. It stops at the first stage that fails and tells you the gap. Steps whose "done when" needs your eyes (three different innkeepers) come back as NEEDS HUMAN with a checklist; you confirm, it continues.

## When a decision is made

A decision is anything you would want the reasoning for in six months: which endpoint, which transport, why no DI container. Two sizes:

- Small: one line in PLAN.md's "Decisions already made" table, as now. Done.
- Worth the reasoning: `/crew:adr <the decision>`. The challenger has a go at it first, then the librarian writes `docs/decisions/ADR-NNN-*.md` and adds the line to the table pointing at it.

If a decision is later reversed, the same command writes a new record that supersedes the old one. The old one is never edited; a hook blocks it. That is the versioning you asked for: the history stays readable as "we believed X, then Y happened, so now Z".

## When the plan changes direction

Ticking a step is not a new version. A new version is written when one of three things happens: you say "new version"; a step's approach in PLAN.md is rewritten rather than ticked; or a decision is reversed. Then `crew:librarian` with "snapshot" writes `docs/plans/vNN-*.md`: what the plan believed, what changed, why, and which lesson or decision drove it. `docs/plans/INDEX.md` becomes the story of the project.

`git tag step-N` after each finished step is the free complement: a version says why, the tag says exactly what the code was.

## Keeping the memory clean

Every few steps, or whenever the inbox has entries, run `/crew:curate`. The librarian merges duplicates, moves inbox entries into `docs/lessons/`, rewrites the index, and once an area has three or more lessons creates a path-scoped rule so anyone touching those files gets the two-line versions automatically. It reports "Flags for the user": read those, they are decisions only you can make.

## Taking it to another repo

Copy `.claude/skills/crew/` to `~/.claude/skills/crew/` once and it loads everywhere. In the new repo run `/crew:setup`: it creates the docs folders and writes `.claude/rules/team.md` from the template, asking you who writes the code. That one answer changes how the reviewer and the gate behave. Everything else about the new project (build command, plan file, coding rules) goes in that file, never in the agents.

## What to expect in the first week

- The Stop hook will occasionally ask for a lesson that was not worth recording. Answer "no lesson, moving on" and it stops. If it does this often, remove the Stop block from `hooks/hooks.json`.
- The gate will return NEEDS HUMAN on steps 5 to 7. That is correct; those stop conditions need a paid run and your eyes.
- The reviewer will be too formal for small changes. Do not call it for small changes; the main conversation's "one idea, a line on why" is still the default.
- If an agent does something odd, the fix is almost always in `.claude/rules/team.md`, not in the agent.

## One-line reminders

- Challenge before you build. Review after you build. Gate before you tick.
- Lessons need a dead end. Facts go in the plan notes.
- Decisions get a line in the table; the ones with reasoning get a record. Records are never edited.
- Versions are for changes of direction, not for progress.
- When in doubt, the main conversation is still the cheapest, fastest tool you have.

## First session: a guided run-through

Do this once, in order, in the Desktop app with the NpcForge folder open. Twenty minutes. Each step proves one thing, and you should see exactly what is described; if you do not, stop and say so.

**0. Load.** Start a new session from the repo root. Trust the workspace if asked. You should see nothing about the crew yet; it is loaded silently. Type `/` in the prompt box: the menu should list `crew:lesson`, `crew:step-done`, `crew:adr`, `crew:curate`, `crew:setup`. If they are missing, the plugin did not load: check the folder is `.claude/skills/crew/` with `.claude-plugin/plugin.json` inside, then restart the session.

**1. The cheap agent.** Type: `Use crew:test-runner to run the tests.` The Tasks pane (Views menu) shows a subagent row while it runs. You get back a short block: build OK, tests Passed 2 Failed 0. Nothing else. That silence is the point: the noise stayed in the subagent.

**2. The wall.** Type: `Change the status line in docs/decisions/ADR-001-crew-and-living-memory.md to "draft".` Claude should try, be blocked by the hook, and tell you the file is immutable and how to supersede it. If it succeeds, the hook did not fire: check that `powershell.exe` is on your PATH and that the workspace is trusted.

**3. The gate on a finished step.** Type: `/crew:step-done 4`. Watch the order: gate first (should PASS, step 4 is done and tested), then reviewer (expect PASS or a couple of warnings on a 115-line codebase), then the lessons question (probably nothing new), then it will notice step 4 is already ticked, then the two decision questions (answer no), then a report. You have now seen the whole ritual on a safe step, so when step 5 is real you know what to expect.

**4. The challenger.** Type: `Sketch McpToolSource.ConnectAsync for step 5 as PLAN.md describes it, then challenge the sketch.` You get a sketch, then a separate report: the proposal stated fairly, one strongest objection, a verdict. Read the objection. Whether it holds or not, notice that it came from something that had not read the sketch's reasoning.

**5. A lesson, end to end.** Type: `/crew:lesson the OpenAI Responses endpoint is required when reasoning is on; Chat Completions returns 400 with function tools`. It will read the template, notice this is already lesson 001, and should tell you so rather than write a duplicate. That is the librarian's dedupe instinct working through the skill. If it writes a duplicate anyway, run `/crew:curate` and watch the librarian merge them.

**6. The Stop hook, deliberately.** Type: `I just spent ten minutes on a 400 from Anthropic because I passed Temperature in ChatOptions; removing it fixed it. What was going on?` Claude explains. At the end of its turn the hook should fire once and Claude should add a line recording the lesson or noting it under the current step, then end with "Lesson recorded: ..." or "Noted under step ...". That is the net catching a debugger discovery you reported in chat. (This one is already in Program.cs as a comment, so expect it to be noted rather than filed.)

**7. Read what was made.** Open `docs/lessons/INDEX.md`, `docs/decisions/INDEX.md`, `docs/plans/INDEX.md`. Three short files, one line per item. That is the whole memory system from the outside.

Then commit, and the next session is step 5 for real.
