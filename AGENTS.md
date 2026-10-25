# AGENTS.md

NpcForge writes non-player characters for tabletop roleplaying games. Dice settle who the
character is; a model writes them. A .NET console app and an MCP server, two processes.

## Why this repo exists

Two goals:

1. **A real product.** A GM describes a scene and gets back a character whose variety comes from
   dice, with levers, anti-levers and consequences that trace back to the brief. `README.md` says
   what and why.
2. **A practice ground for AI engineering.** I am the architect. I set the task, the constraints
   and what done looks like, make the decisions, and judge the evidence. Agents write the code.
   The skill I am practising is directing agents well and knowing what to check.

Steps 1 to 8 were typed by hand, to learn how an agent loop, tools and MCP fit together. That
phase is over. Do not hand me sketches to type. ADR-006 records the change.

## How we work

**Work is described as epics: Goal, Constraints, Done when.** `ROADMAP.md` holds them, with a
template. A task I hand over in chat has the same three parts. I describe what I want; you, with
pstack, decide how to build it.
- "Done when" is something you can check: a command, a test, a file. Anything only I can judge
  ("does this read as comic?") is marked *needs my eyes*, and you list it for me at the end. So is
  any change to what I see or how I start the app, such as console output or a Visual Studio launch
  profile, even when a test covers it.
- Work until "Done when" holds, then report the evidence: what you ran and what it showed. A clean
  build is not evidence that it works. For a new test, show it fail with the code it guards removed.
- If an epic or task is ambiguous, or a constraint blocks it, stop and ask. Do not widen the scope.
- When an epic is done: shrink it to one line under "Done" in `ROADMAP.md`, bring
  `docs/architecture.md` (and README's "Status") into line with the code, add the epic to the
  build history table there, and write a lesson for each dead end the epic hit, all in the epic's
  last PR.

**Ceremony matches risk.**

| The change | What it needs |
| --- | --- |
| Easy to undo, and covered by tests | Do it. Report the evidence. |
| Changes the MCP contract (`tools/list`), the save format, or a recorded decision | A design check first (pstack `architect` or `interrogate`), then an ADR |
| Costs money: any run against a real model | Ask first, with the command and why |
| Commits on your own branch, pushing that branch, opening a PR | Do it. One branch per task, never `master`. The PR is where I review |
| Merging, landing a stack, tags, anything on `master` | I do these. Never merge or land unless I ask in so many words |

**Branches and PRs.** pstack's coding agents start their branches from GitHub's `master`, not the
local one, so I push `master` before handing over a task. A PR targets `master`, says how the change
was proved, and is opened ready for review. I review it and merge it on GitHub.

**My saves.** Uncommitted changes to `NpcForge.Server/characters.json` are characters from my own
runs. Replays, recordings and tests save to temp files; for any other run of yours, set
`NPCFORGE_CHARACTERS` to a scratch file, so anything in `characters.json` is mine. When you find
them, commit them on the branch you are working on, in a commit of their own (`chore(saves): ...`),
and mention it in the PR. Never revert, reset or overwrite them.

**Tooling.** [pstack](https://github.com/michael-denyer/pstack-claude) is a workflow plugin: its
`poteto-mode` skill routes any task that touches more than one file, involves a design choice, or
is a bug of unknown cause. Its `architect` skill settles a design before code, and `interrogate`
has several models try to break a diff. It is enabled for this repo in `.claude/settings.json`,
and its commands carry the prefix `/pstack:`. `docs/pstack.md` says how to use it here. Without
it, do the same by hand: check the design before coding, and have a fresh subagent review the diff.

To iterate, use `/goal <the epic's Done when>, or stop after N turns`, with N = 6 unless the epic
says otherwise. Always give a stop clause, as the app's own loop has a turn cap.

**Never use Fable** (`fable`, `claude-fable-*`) for anything: not as a subagent, a review panel
member or a judge. It is outside the budget. Use `opus` for the hardest judgement, `sonnet` for
implementation and `haiku` for cheap checks. pstack's defaults name Fable; the model sheet at
`~/.claude/pstack-models.md` overrides them, and a hook refuses any agent started on Fable.

**Record what was learned, once.**
- A decision with reasoning worth keeping: a new ADR in `docs/decisions/` and a line in its
  INDEX. Records are never edited; a new one supersedes an old one.
- A problem that took a dead end to solve: a lesson in `docs/lessons/` and a line in its INDEX.
- Anything else: nowhere, unless the next agent would get it wrong without it.

## Rules that must not break

- **`roll_character` is never shown to the model.** The app calls it directly and puts the brief
  in the first message. The same goes for `save_character` and `load_character`. A tool the model
  can see is a tool it can skip. (`McpToolSource.AppOnly`)
- **The server never writes to stdout.** Stdout is the MCP transport. Logs and traces go to stderr.
- **Two processes, no shared contracts project.** JSON over the wire is the contract. The console
  has no `CharacterBrief` type.
- **The model never guesses. It is either told or it rolls.** Variety comes from the server's dice,
  never from asking the model to invent.
- **Never parse the model's prose.** Its markup differs by model. Code checks only structured data:
  the brief, the save file, and the arguments of a tool call.
- **`NpcForge.Console/skills/npc-writer/SKILL.md` is product, not documentation.** It is what the
  model is told. Changing it changes the app's behaviour, so treat it like code.
- **No XML comment may contain `--`** (`.csproj`, `.props`, `.slnx`). The project stops loading
  (lesson 003).

## Code

- Plain over clever. Readable in six months by someone who did not write it.
- Modern C# where it names things, not where it only compresses.
- No abstraction until there are two things to abstract.
- No Clean Architecture layering. Two interfaces earn their place: `IChatClient`
  (Microsoft.Extensions.AI) and `IToolSource`. The "Ready to grow" epic brings layers,
  Clean Architecture by preference, with an ADR first.
- No host or DI container in the console. `Program.cs` wires things by hand so the order of events
  stays readable: connect, roll, run, save. The same ADR may change this.
- Why-comments, not what-comments: a comment says which rule a line keeps. Keep them: never run
  pstack's `/no-comments` or its `comment-sicko` agent here
  (`.agents/playbooks/npcforge-keep-why-comments.md`). Comments that mention "step N" mean the build
  history table in `docs/architecture.md`.

## Vocabulary

- A **character brief** is the settled list of traits. Not a seed.
- **Rolling up a character** is producing that brief.

## Build and run

```
dotnet build
dotnet test                                        # free: no network, no model calls
dotnet run --project NpcForge.Console -- --load 3  # no model call; still needs an API key set
dotnet run --project NpcForge.Console              # paid: OpenAI, default model
dotnet run --project NpcForge.Console -- --provider anthropic --model claude-opus-5   # paid
```

- `--seed <n>` fixes the dice. `--record <file>`, which needs `--seed`, also writes a paid run to a
  file, and `--provider replay --recording <file>` answers from that file for free.
  `docs/runs/README.md` has both commands for the replay fixture.
- API keys are user-secrets on the console project: `OpenAI:ApiKey` and `Anthropic:ApiKey`.
- A build fails while the app is running or paused in the debugger: it locks `NpcForge.Console.dll`.
- Tests that start the server take several seconds each.

## Where things are

| | |
| --- | --- |
| `README.md` | What NpcForge is, and why it is shaped this way |
| `ROADMAP.md` | What's next, as epics, with a template for writing one |
| `docs/architecture.md` | How it fits together, and behaviour worth knowing |
| `docs/decisions/` | ADRs. `INDEX.md` is the live list of every decision |
| `docs/lessons/` | Problems that took a dead end. `INDEX.md` is loaded into every session |
| `docs/runs/` | Records of paid runs: the evidence for anything about model behaviour |
| `docs/pstack.md` | How to use the pstack workflow plugin in this repo |
| `.agents/playbooks/` | NpcForge's changes to pstack's playbooks |
| `.claude/skills/verify/` | How an agent drives the app and proves a change, for free |
