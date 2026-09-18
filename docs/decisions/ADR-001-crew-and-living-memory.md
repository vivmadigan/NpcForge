# ADR-001: A crew of specialist agents, with versioned decisions and curated lessons

- **Status:** accepted
- **Date:** 2026-09-18
- **Supersedes:** none
- **Plan version:** v01

## Context
Steps 1 to 4 are done and the project is about to grow a second process (the MCP server), which is where the non-obvious problems start. Until now Claude has worked in one conversation with `AGENTS.md` as its only standing instruction. Two things were being lost: the reasoning behind decisions once `PLAN.md`'s table was updated in place, and the debugging discoveries that lived only in a chat window until they were copied by hand into the step notes. The user also wants the same working method in other repositories, not only this one.

## Options considered
- **A** Keep one conversation, keep writing notes into `PLAN.md` by hand: no new machinery, but nothing enforces the notes and nothing separates the judge from the worker.
- **B** A hierarchy of agents including an implementer that writes the code: standard multi-agent setup, but it contradicts `AGENTS.md` ("finished code handed to me is worth nothing here").
- **C** A flat crew of judging and remembering agents (reviewer, challenger, step-gate, test-runner, librarian) packaged as a portable plugin, with project specifics confined to one team rule file, plus hooks that make decision records immutable and check for unrecorded lessons.

## Decision
C.

## Why
The user writes the code, so the value of agents here is in independent judgement and in memory, not in typing. Independence comes from fresh context: the challenger and the step-gate never see the reasoning that produced what they judge. Memory comes from files in the repository, curated, because Claude Code's own auto memory is machine-local and skips debugging fixes by design. Immutability is enforced by a hook rather than by instruction because instructions are context Claude tries to follow, and a hook is a wall. The plugin form is what makes the archetypes travel: the agents contain nothing about this project and read `.claude/rules/team.md` for everything specific.

## Consequences
- Easier: reviews are grounded in the project's own rules and cite lessons; a step cannot be ticked on say-so; the history of why the plan changed is readable without `git log`; the dead ends behind a fix are kept, which the plan's own notes never did.
- Harder: every turn ends with a small-model check for unrecorded lessons, which costs a little and can occasionally ask for a lesson that was not worth recording. The reviewer and the challenger run on the most expensive model. There are now two places a decision can be written (the plan's table and `docs/decisions/`); the table is the register and the record is the appendix, and keeping that straight is the librarian's job.
- Known thin spots at the time of writing: the plugin has been filled in for one project; the retry loop for code is the user by design and `/goal` covers the rest; an independent review of this setup recommended dropping plan versions and the Stop hook as more ceremony than a one-person project at step 5 needs. Both were kept because they were asked for; they are the first two things to remove if they turn out not to earn their place.
- Revisit if: the plugin's agents start accumulating project-specific text (the team rule should absorb it instead), or the inbox stays empty for several steps (the capture prompt is then too conservative), or the number of agents grows past five without a clear gap they fill.
