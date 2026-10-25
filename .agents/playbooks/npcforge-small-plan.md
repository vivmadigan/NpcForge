---
extends: multi-phase-plan
when: Any NpcForge epic or task that spans more than one PR.
---

# NpcForge: a plan sized to the project

NpcForge is a small console app with one owner, who also uses it to practise directing agents the
professional way. Keep the habits that catch real bugs: a design check before a risky change, tests
shown to fail with the code they guard removed, one PR per task, and the owner's review before any
merge. Drop the ceremony built for large programs: a plan file in a fixed skeleton, ten live swarm
lanes per PR, a perf gate, a review video, and an hourly audit loop. Here they cost more than they
find. Say in one line which steps you skipped and why.

- **Replace** "Copy the skeleton below into the plan file and fill every placeholder. Unless the operator names a path, write the file under the agent store (`~/.claude/orchestrate/<slug>/docs/`). Keep every heading and every sub-block in the order shown. One section per PR. One PR is one change with its own evidence (the **sequence-verifiable-units** principle skill). Name the execution playbook in **How to read this**. Pick between `playbooks/autopilot-full.md` and `playbooks/autopilot-stack.md` per the rule at the end of `playbooks/autopilot-stack.md`. A standing program takes `playbooks/orchestrate.md`. The execution playbook owns base selection, topology changes, and merge authority. Do not copy its rebase steps into the plan." with: Do not copy the skeleton. Write the plan in chat as one table, a row per PR in order: what it does, whether it is a restructure or a new behavior (ROADMAP.md keeps those in separate PRs), what it waits for, and the Done-when check it proves. One PR is one change with its own evidence (the **sequence-verifiable-units** principle skill). Under the table, name each decision you made and each step you skipped. To execute, give each PR to one fresh `pstack:poteto-agent` running the Feature, Bug fix or Refactoring playbook in its own worktree. Start the PRs that touch different files and need nothing from each other together; the rest wait until the owner merges what they depend on. The last PR of an epic carries its close-out (AGENTS.md "When an epic is done").
- **Replace** "Run `node skills/poteto-mode/scripts/check-plan.mjs <plan.md>` from the installed plugin and fix every line it prints" with: Skip the plan check script. It checks the skeleton this playbook drops
- **Replace** "Post the plan path and the script's output, then stop." with: Post the table, then stop. A go already in the request ("plan the PR order and go") counts as the operator's go.
- **Replace** "Tests alone are not sufficient verification. A PR is verified only when its unit, live, and perf boxes are all checked (the **prove-it-works** principle skill)." with: A PR is verified when its new tests pass and each was shown to fail with the code it guards removed, `dotnet test` passes, and the project `verify` skill passes (the **prove-it-works** principle skill). The rest of this paragraph does not apply here: no live swarm lanes, no perf gate unless the epic is about speed, and no review video. Anything only the owner can judge is marked *needs my eyes* and listed in the PR (AGENTS.md).
