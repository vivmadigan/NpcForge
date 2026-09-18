# crew

A portable team of specialist agents for Claude Code, plus the skills and hooks that keep a project's decisions versioned and its hard-earned lessons from being lost.

The agents know nothing about any particular project. Everything project-specific lives in one file per repository, `.claude/rules/team.md`, which every agent reads first. That is what makes the crew travel. It has been filled in for one project so far; expect to adjust the team rule, not the agents, when something feels off in a new repo.

## The crew

| Agent | Model | Can edit? | Job |
| --- | --- | --- | --- |
| `crew:reviewer` | opus | no | Reviews changed code against the project's own rules and recorded lessons. A Critical finding that rests on judgement (not on a failing build) is challenged before it is reported. Keeps a memory of this codebase's recurring patterns. |
| `crew:challenger` | opus | no | Fresh-context second opinion. Steelmans a proposal or finding, then gives the strongest honest objection and a verdict: HOLDS, WEAKENED, REFUTED. |
| `crew:step-gate` | sonnet | no | Decides whether a plan step's "done when" holds, from evidence it gathers itself. PASS, FAIL, or NEEDS HUMAN with the exact list of what the human must confirm. |
| `crew:test-runner` | haiku | no | Runs the build and tests, reports only failures. Keeps the noise out of the main conversation. |
| `crew:librarian` | sonnet | yes | Curates lessons, writes decision records and plan versions, keeps the indexes current. The only agent that writes files. |

Why the models are split this way: a weak reviewer produces confident false approvals, which is worse than no reviewer, so judgement gets the strongest model. Running tests and filing documents is mechanical and verifiable, so it gets the cheap ones. Cost to be aware of: the reviewer and the challenger are both on the most expensive model, and the reviewer can spawn the challenger. Use the reviewer when you ask for a review or finish a step, not after every edit.

Why there is no implementer: the crew was built for a project where the human writes the code and agents explain, sketch and review. If a project wants agents to implement, say so under "Who writes the code" in the team rule; the reviewer and gate work the same either way.

Why there is no CI/CD or deployment agent yet: there is no pipeline to be expert in. Add one when there is, as a sixth file in `agents/`. The "documentation agent" is the librarian.

## The skills

| Command | What it does |
| --- | --- |
| `/crew:lesson <one line>` | Records a hard-earned lesson to `docs/lessons/inbox/` and adds it to the index. Do this the moment a non-obvious problem is solved. |
| `/crew:step-done [step]` | The ritual for finishing a plan step: gate, review, lessons, tick the plan, record decisions, snapshot only on a concrete trigger. Refuses to tick on say-so. |
| `/crew:adr <decision>` | Records a decision as a new immutable record, after the challenger has had a go at it. |
| `/crew:curate` | Asks the librarian to drain the inbox into the curated lessons. |
| `/crew:setup` | Bootstraps a new repository: docs folders, indexes, team rule, CLAUDE.md import. |

## The hooks

**Immutable records.** A `PreToolUse` hook blocks any `Edit` or `Write` to an existing file under `docs/decisions/` or `docs/plans/`, except `INDEX.md` and except an edit that marks a record `Superseded by`. Creating a new file is always allowed; that is how a decision changes. Instructions are context Claude tries to follow; a hook is a wall.

The hook runs `scripts/protect-docs.ps1` through `powershell.exe`, so it works on Windows with no extra tools. On macOS or Linux, either install PowerShell (`pwsh`) and change `powershell.exe` to `pwsh` in `hooks/hooks.json`, or replace the entry with `{"type": "command", "command": "bash", "args": ["${CLAUDE_PLUGIN_ROOT}/scripts/protect-docs.sh"]}` (needs `jq`). Known gap: a shell redirect (`echo > docs/decisions/x.md`) is not caught. The reviewer will see it in the diff.

**Lesson check.** A `Stop` hook runs a small, fast model at the end of every turn in the main conversation. It sees only the turn's final message, not the files, so it asks one narrow question: does this message describe a non-obvious problem that was solved, without saying where the lesson was recorded? If yes, Claude gets one more turn to record it. The team rule tells the lead to end such turns with `Lesson recorded: <path>` or `Noted under step N`, which is what the hook looks for. It is written to be conservative and will say nothing on most turns. Cost: one small-model call per turn. If it gets in the way, delete the `Stop` block from `hooks/hooks.json` and run `/reload-plugins`.

Why this lives on the main session's `Stop` and not on `SubagentStop`: in a project where the human writes the code, lessons are found in the human's debugger and reported in chat. The main conversation is where they surface, so that is where the net is.

## Loops

Two kinds, and it matters which one you mean.

The retry loop for **code** is the human, by design, in a project where the human writes the code: sketch, type it, `crew:test-runner` reports, fix, again. Nothing here automates that, on purpose.

The retry loop for **work the lead may do itself** (a test, a sketch, a document, a migration in a project that allows it) is Claude Code's `/goal`: type a condition, and a separate model judges after every turn whether it holds, so the worker cannot declare itself done. Always include a stop clause: `/goal dotnet test passes with zero failures, or stop after 6 turns`. For fan-out over many files, use a workflow (`ultracode`); the crew's agents can be its workers.

## The documents

```
docs/
  decisions/   ADR-NNN-*.md     full records; written once, superseded never edited
  plans/       vNN-*.md         snapshots of the plan's direction, written on a concrete trigger
  lessons/     NNN-*.md         curated lessons; INDEX.md is imported into CLAUDE.md
    inbox/                      where new lessons land
.claude/rules/
  team.md                       the one project-specific file the crew reads
  lessons-<area>.md             appears only once an area has three or more curated lessons
```

The plan file itself (`PLAN.md` or whatever the team rule names) is never frozen. It is the living document, and if it has a decisions table, that table is the live register; a record in `docs/decisions/` is the full account behind a line in it. A plan version is written only when the user says "new version", a step's approach is rewritten, or a decision is reversed. Ticking a step is not a version; `git tag step-N` is the free way to mark that.

Why lessons exist alongside the plan's per-step notes: the notes record what turned out to be true. They never record the dead ends. A lesson file has a mandatory "What did not work" section, and that is the part that saves the next hour. If a problem had no dead ends, it was not a lesson; put the fact in the plan's notes and move on.

Why lessons live in the repo and not in Claude Code's auto memory: auto memory is machine-local and skips debugging fixes by design. Lessons are exactly debugging fixes, and they need to be reviewable, versioned, and shared.

## Taking the crew to another repository

Two options.

**Everywhere, once:** copy this folder to `~/.claude/skills/crew/`. Claude Code loads it in every project as `crew@skills-dir`, no install step. Then in each new repo run `/crew:setup`.

**Per project:** copy this folder to `<repo>/.claude/skills/crew/` and commit it. Loads after you trust the workspace, and only when Claude Code is started from the repository root, not a subfolder. Same `/crew:setup` afterwards.

If both copies exist, keep them identical or delete one; a project copy and a personal copy with the same name is confusing.

## Trying it out

1. Start a session in the repository root. `/context` should list the five agents under Custom Agents and the five skills under `crew:`.
2. Ask: "Use crew:test-runner to run the tests." You should get a short report with only failures, or none.
3. Try to edit `docs/decisions/ADR-001-*.md`. The hook should block it with a message.
4. `/crew:step-done 4` on a step already finished should return PASS from the gate and a review.

## Turning things off

- One agent: add `"Agent(crew:reviewer)"` to `permissions.deny` in `.claude/settings.json`; if that does not take, the bare name `"Agent(reviewer)"` is the other spelling to try.
- The Stop hook: remove the `Stop` block from `hooks/hooks.json`, then `/reload-plugins`.
- The whole plugin: `claude plugin disable crew@skills-dir`, or delete the folder.

## Not included on purpose

Agent teams (several agents messaging each other live) are off by default in Claude Code and stay off here: they are experimental and expensive. When a problem genuinely needs agents arguing with each other, set `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1` for that session and ask for a team. The challenger is the everyday, cheaper version of the same idea: one fresh mind, one strong objection.
