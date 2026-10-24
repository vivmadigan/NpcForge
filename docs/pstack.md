# Using pstack

pstack is a workflow plugin for Claude Code. You give it a goal and a way to check the goal is
met. It picks a step-by-step playbook, hands the coding to subagents, reviews their work, and
proves the result on the real thing before it calls the work done. Lauren Tan (poteto) wrote it
for Cursor; Michael Denyer ported it to Claude Code.

Every claim here was checked against the installed plugin (version 0.9.102), its documentation,
or a command run on this machine on 2026-10-24. Sources are listed at the end.

## What is installed

- **One router skill, `poteto-mode`.** It matches your task to one of 23 playbooks and runs the
  other skills as the steps need them.
- **41 skills you can call by name** and 24 `principle-*` skills, one rule each, that
  `poteto-mode` reads and cites.
- **4 agents.** `pstack:poteto-agent` writes code for a playbook; the others review.
- **1 hook.** At the start of every session it tells Claude to route certain tasks to
  `poteto-mode` (below).
- **Scope: NpcForge only.** It is enabled in this repo's `.claude/settings.json` and off in
  other projects.
- **Always-on cost: about 5,500 tokens per session**, from `claude plugin details
  pstack@pstack-claude`. A skill costs more only when it runs, and the real spend is the
  subagents it starts.

## How it starts

**On its own.** The session hook tells Claude to use `poteto-mode` when a task:
- touches more than one file, or changes a signature other files call;
- involves a design or architecture choice;
- is a bug whose cause is not known yet, or a performance issue.

Smaller tasks (one file, a question, a one-line edit) are done directly.

**By name.** In Claude Code every pstack command carries the plugin prefix: type
`/pstack:poteto-mode`, `/pstack:how`, and so on. pstack's own examples write `/poteto-mode`; on
Claude Code, use the prefixed form.

**Your loop, start to finish:** push `master`, hand over an epic from `ROADMAP.md` (or a task with
a goal, constraints and a done check), approve any paid run when asked, review the PR it opens,
merge it on GitHub, then pull `master`.

**Mid-chat**, start a message with "new task" when the subject changes. Otherwise `poteto-mode`
treats your message as the next step of the last task.

**AGENTS.md wins.** The hook's own text says the user's instructions (CLAUDE.md, AGENTS.md, direct
requests) take precedence over pstack. Where the two disagree here, see "Where pstack meets
NpcForge's rules" below.

## Writing a request

pstack's own advice, in short. It matches how an epic is written in `ROADMAP.md`: the goal is the
Goal, the real constraints are the Constraints, and the done check is the Done when.

**Put in:**
- the goal: what is wrong, or what you want;
- a done check that can pass or fail ("make it better" is not one);
- the proof you want to see: the real command output, the stored value, a before and after number;
- what you already know: a symptom, a repro step, a log;
- the real constraints, such as "repro first", "don't change any code yet", "zero behavior
  change", "let me review before proceeding".

**Leave out:**
- how to do it;
- a list of skills or steps. A hand-written order drops steps the playbook would keep. Name a skill
  only to override one choice;
- your theory of the cause, until the agent has restated the problem.

**Steer in one line** when a run drifts: restate the goal ("i asked for a repro, not a fix yet"),
or name a principle ("apply prove it works. show me the real output, not the build log"). Follow-ups
can be as short as "continue" or "keep going until done".

## The playbooks you will use

You don't pick these by name. `poteto-mode` picks one from your description, copies its steps into
a checklist, and marks any step it skips with `skip: <reason>`.

| Playbook | For | What it does |
| --- | --- | --- |
| Feature | New or changed behavior | `how` over the code, `architect` the design, plan the parallel work, delegate the code to a subagent in its own worktree, verify on the real surface, small ordered commits, `interrogate` if the design is contested, open a PR |
| Bug fix | A reported defect | Reproduce it first, narrow down the cause with evidence, `architect` if the fix crosses a function boundary, delegate the fix, rerun the repro, failing test committed before the fix |
| Refactoring | Same behavior, new structure | Pin current behavior with a test or a recorded baseline first, remove dead code before adding, move in small steps that keep the pin green, prove nothing changed, revert if it doesn't make the code easier to read |
| Investigation | A read-only question | `how` (and `why` for reasons), then a cited answer or a recommendation. No code, no PR |
| Prototype | A design decision | Throwaway versions in a scratch folder, compared on evidence. The real build then goes through Feature |
| Autonomous run | A long task you step away from | States the exit condition as a check, loops until it passes, keeps a decision log |

Feature, Bug fix and Refactoring all end with **Opening a PR**: small ordered commits, a PR titled
`type(scope): subject` with a short description, opened ready for review with the GitHub CLI (`gh`,
logged in here as `vivmadigan`).

## Skills worth calling by name

The default is `/pstack:poteto-mode`, which runs most of these for you. Call one directly when you
want more or less of something than the playbook gives.

| Command | Use it to |
| --- | --- |
| `/pstack:how` | Learn how code works now, or where new code should live |
| `/pstack:why` | Learn why code is shaped this way |
| `/pstack:teach` | Get `how` and `why` explained plainly, or have a choice argued |
| `/pstack:sharpen` | Aim a task better before starting |
| `/pstack:in-your-own-words` | Check the agent understood your goal before it starts |
| `/pstack:architect` | Settle types and module shape before code. Add "with checkpoint" to review the design first |
| `/pstack:arena` | Several attempts at one brief, with the best parts merged |
| `/pstack:interrogate` | Have several models review a diff and try to break it. It suggests; it changes nothing |
| `/pstack:blast-radius` | Find what a small change could break outside its own diff |
| `/pstack:tdd` | Fix a bug test-first, when a cheap local test exists |
| `/pstack:recall` | Catch up on your recent work on a topic, across chats |
| `/pstack:ketchup` | Catch up on this chat since your last message, with every decision waiting on you |
| `/pstack:bro` | Hear the last reply again in plain words |
| `/pstack:show-me-your-work` | Keep or read a decision log for long or unattended work |
| `/pstack:reflect` | After a task, turn what it taught into skill edits you approve |
| `/pstack:correct` | Make a mistake agents keep repeating impossible: architecture, a type, a lint or a test first, docs last |
| `/pstack:poteto-help` | Ask which skill or playbook fits. It answers and hands you a prompt; it doesn't start the work |
| `/pstack:setup-pstack` | Change the model or effort per role, or turn routing off |

Close calls: `how` is what the code does, `why` is the reasons. `arena` gives every worker the same
brief; `swarm` splits work into slices. `interrogate` reviews the diff; `blast-radius` looks
outside it.

## Example workflows in NpcForge

These are illustrations built from pstack's own example prompts. Swap in the real details.

**An epic (Safety net).**
```text
/pstack:poteto-mode Do the Safety net epic in ROADMAP.md. Its "Done when" is the done check.
Ask me before the paid recording run. Show me the real dotnet test output, and each of the
three break-it checks failing and then passing.
```
Expect the Feature playbook: a design pass, a delegate writing the code in its own worktree, then
evidence and a PR for you to review. You decide the paid run.

**A restructure (Ready to grow).**
```text
/pstack:poteto-mode Do the Ready to grow epic. Zero behavior change: the replay test must pass
unchanged and must not be re-recorded. Show me the test output before and after.
```
The Refactoring playbook pins behavior before moving anything, and the Safety net's replay test is
exactly that pin.

**A bug.**
```text
/pstack:poteto-mode <symptom>. Repro first, then fix and verify.
```

**Understand before deciding.**
```text
/pstack:how how does a roll get from Program.cs to the dice?
/pstack:why why is a saved character the brief and the text together?
```

**Design before building (one task from Better characters).**
```text
/pstack:poteto-mode We need the tone task from the Better characters epic. /pstack:architect it
first, and answer open questions with prototypes. Let me review before proceeding.
```

**Review before you commit.**
```text
/pstack:interrogate the whole branch, but skeptically. Don't change anything yet. No nitpicks
unless it's a real bug or regression.
```
Read its Dismissed list as well as its Act On list.

**Step away while it works.** Claude Code's `/goal` keeps a session working until a condition holds.
After each turn a small, fast model (Haiku by default) judges the condition from what is in the
conversation. It cannot run commands or read files, so the condition must be something Claude's own
output shows. A "stop after N turns" clause is judged the same way, so it is a soft limit, not a
hard one.
```text
/goal dotnet test passes with zero failures and each break-it check has been shown failing,
or stop after 20 turns
```
pstack's own recipe for a long unattended run is the Autonomous run playbook: say "im going to bed",
give the goal and done checks, ask for a fresh worktree and a decision log, and use Claude Code's
`/loop`. Using `/goal` and `poteto-mode` together has not been tried in this repo yet.

**After the task.**
```text
/pstack:reflect capture what we learned so the next run doesn't repeat it.
```
Approve only the edits that would change a future decision.

## Cost and models

- **Models per role** come from `~/.claude/pstack-models.md`. Fable is never used: the sheet replaces
  it with `opus`, a hook in your user settings refuses any agent started on Fable (tested), and the
  same settings limit model choice to opus, sonnet and haiku (`availableModels`).
- **Code delegates:** `sonnet` for features and refactoring, `opus` for bug fixes.
- **Review panels** run one subagent per model in their list. `interrogate`, `architect` and `arena`
  each run three here (opus, sonnet, haiku). pstack's default is four, including Fable.
- **`how`, `why`, `swarm` and `reflect`** use `opus`.
- **To spend less:** pick a lower effort or cheaper models with `/pstack:setup-pstack`; shorten a
  panel list (one subagent per entry); set a role to `inherit-parent` so it runs on the chat's model,
  which saves money only when the chat runs on a cheaper model; save `poteto-mode` for work that
  needs rigor.

## Where pstack meets NpcForge's rules

- **Comments.** pstack's `/no-comments` step deletes any comment that isn't about something the code
  can't change. NpcForge keeps why-comments, so the project playbook
  `.agents/playbooks/npcforge-keep-why-comments.md` removes that step. pstack's own checker passes
  it: `node <plugin>/skills/poteto-mode/scripts/check-playbooks.mjs` from the repo root. `/deslop`
  still runs before commit. It removes comments that are unnecessary or don't match the file's
  style, so the same playbook tells it to keep every comment that names a rule.
- **Commits and PRs.** Agents commit on their own branch (never `master`), push it, and open a PR,
  as pstack's playbooks do. You review the PR and merge it on GitHub, and you do tags and anything on
  `master`. pstack's Babysit playbook drives a PR to merge-ready and stops; it merges only when you
  ask it to merge, land or ship. One task is one PR of small ordered commits, not a stack, because
  you squash-merge (`.agents/playbooks/npcforge-one-pr-per-task.md`).
- **Push `master` first.** pstack's coding agents work in their own worktree, which Claude Code
  branches from GitHub's `master` unless the `worktree.baseRef` setting is `"head"`. Commits that are
  only on your machine are invisible to them, so push `master` before you hand over a task.
- **Paid runs.** pstack goes ahead with reversible work and external actions without asking.
  AGENTS.md says any run against a real model needs your yes first. Before a paid or other costly
  step, a free stand-in rehearses the pipeline and `interrogate` reviews it, even when the design was
  decided (`.agents/playbooks/npcforge-costly-steps.md`). The project `verify` skill drives the app
  for free.
- **Irreversible actions.** pstack always pauses before a force-push to a shared branch, a deploy,
  deleting data, or a customer message.
- **Its working files** stay out of git (`.gitignore`): the playbook checklist in `todo.md` (used
  when the session has no task tools), and the decision log in `decisions.tsv`, or under `.audit/`
  when several efforts run at once.
- **Doesn't apply here, or needs more installed:** `typescript-best-practices` is for TypeScript.
  The `watch-pr` and `orch` scripts behind the Babysit, Shipping and Orchestrate playbooks need Bun,
  which isn't installed. `reflect`, `automate-me` and skill authoring take their guidance from
  Anthropic's `plugin-dev` plugin, which isn't installed either.

## Upkeep

- **Update:** `claude plugin marketplace update pstack-claude`, then
  `claude plugin update pstack@pstack-claude`. pstack's upstream guide notes that Claude Code doesn't
  auto-update third-party marketplaces.
- **After an update:** rerun the playbook checker above; it fails if an update rewrote a step this
  repo's playbook quotes. Check `~/.claude/pstack-models.md` for new roles: a role missing from the
  sheet falls back to pstack's default, which may be Fable (the hook still blocks it).
- **Plugin commands from a terminal** need Claude Code 2.1.292 or later. The `claude` on your PATH
  (winget) is 2.1.268; the desktop app runs 2.1.304 and the VS Code extension 2.1.310.
- **Turn routing off:** `/pstack:setup-pstack`, or add `session hook: off` to the model sheet.
- **Switch pstack off in this repo:**
  `claude plugin disable pstack@pstack-claude --scope project`, run from the repo folder.
- **Your data:** pstack has no server and no telemetry. Whatever its skills read goes to your model
  provider, like any other Claude Code work.

## Sources

Installed plugin: `~/.claude/plugins/cache/pstack-claude/pstack/0.9.102/`, public copy at
[github.com/michael-denyer/pstack-claude](https://github.com/michael-denyer/pstack-claude/tree/main/plugins/pstack).

- Router, principles, autonomy, models: `skills/poteto-mode/SKILL.md`
- Playbooks: `skills/poteto-mode/playbooks/` (`feature.md`, `bug-fix.md`, `refactoring.md`,
  `investigation.md`, `prototype.md`, `autonomous-run.md`, `opening-a-pr.md`)
- Prompting advice and example prompts: `skills/poteto-help/SKILL.md`,
  `skills/poteto-help/references/prompting.md`, `skills/poteto-help/references/recipes.md`
- Routing rules: `hooks/session-start-context.md`
- Review panel: `skills/interrogate/SKILL.md`; comment removal: `skills/no-comments/SKILL.md`,
  `skills/deslop/SKILL.md`
- Project playbook format and checker: `skills/poteto-mode/SKILL.md` "Project playbooks",
  `skills/poteto-mode/scripts/check-playbooks.mjs`
- Command prefix, routing off, dependencies:
  [docs/reference.md](https://github.com/michael-denyer/pstack-claude/blob/main/docs/reference.md)
- Install and data handling:
  [README](https://github.com/michael-denyer/pstack-claude/blob/main/README.md)
- Upstream author's guide:
  [cursor/plugins pstack README](https://github.com/cursor/plugins/tree/main/pstack)
- `/goal`: [Claude Code docs](https://code.claude.com/docs/en/goal)
- Run on this machine: `claude plugin details pstack@pstack-claude`, `claude plugin list`,
  `gh auth status`, `claude --version`, and the playbook checker
