# ADR-006: Agents write the code; the user sets the task, the constraints and what done means

- **Status:** accepted
- **Date:** 2026-10-24
- **Supersedes:** ADR-001

## Context
Steps 1 to 8 were built by hand on purpose. The goal was to learn how an agent loop, tools and MCP
fit together, so the user typed every line from sketches, and ADR-001 put a crew of judging agents
(reviewer, challenger, step-gate, test-runner, librarian) around that. It worked: the loop, the
server, the dice, the skill and saving all landed, and the success test passed on 2026-09-22.

The goal has changed. The user's work now needs less hand-coding and more directing agents well:
setting a task, its guardrails and a concrete "done", then trusting checks rather than typing.
Measured against that, the old workflow cost more than it bought. Between step 8 and step 9 the work
was all planning: two plan versions, two challenger rounds, and step 9 written out as finished code to
be pasted. The documents had grown to about 6,400 lines across README, BUILD, PLAN, four plan
versions, the archived step notes and the crew, and had drifted apart: README said "Nothing built
yet", and ADR-005 holds a line that PLAN.md had to correct.

## Options considered
- **A** Keep the crew and the hand-typed workflow: proven here, but it practises the wrong skill and
  spends the user's time on typing and ritual.
- **B** Keep the crew and let agents write the code: keeps the project's own process, but it is a
  bespoke process to maintain, with the same ceremony on every step whatever its risk.
- **C** Agents write the code. Tasks arrive as cards: Task, Constraints, Done when. Ceremony matches
  risk. pstack, a maintained workflow plugin, replaces the crew. The documents shrink to a small core.

## Decision
C.

## Why
The user's time is the scarce resource, and it is best spent on what agents cannot do for them:
deciding what to build, defining done, and judging evidence. A card whose "Done when" an agent can
check lets the agent iterate to it without the user in the loop, and makes the evidence the thing
reviewed. Matching ceremony to risk keeps design checks and ADRs where they pay (the MCP contract,
the save format, decisions, money) and drops them where tests already prove a change. pstack covers
what the crew did (independent review, design checks, lessons turned into skill edits) and others
maintain it, which is the point: the user practises using a strong toolchain, not building one. Plan
versions go because ADRs already record changes of direction, and git keeps every version of a plan.

## Consequences
- Easier: a new agent reads AGENTS.md and knows the goal and how work is done; a task is handed over
  in minutes; the user reviews diffs and evidence instead of typing.
- Harder: trust has to be earned by checks, so every "Done when" has to be sharp, and breaking code
  on purpose to see a test fail matters more. The user reads more code than they write, and has to
  keep that skill.
- Removed, all kept in git at commit `cd20b60`: the crew plugin and its hooks (the decision-record
  wall and the lesson check at the end of each turn), plan versions, the lessons inbox and its
  curation, `BUILD.md`, `PLAN.md` and `.claude/rules/team.md`. Decision records stay immutable by
  convention, without a hook.
- Revisit if: pstack's routing and review panels cost more than they catch over two or three real
  tasks, or agents miss something the crew's step-gate would have caught.
