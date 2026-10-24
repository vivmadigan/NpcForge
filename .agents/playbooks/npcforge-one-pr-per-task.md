---
extends: opening-a-pr
when: Any NpcForge task that ends by running Opening a PR.
---

# NpcForge: one task, one PR

The owner reviews each PR and squash-merges it on GitHub. A stack then costs a rebase of every
child after each merge, and the owner does the merging. Small ordered commits inside one PR give
the reviewer the same step-by-step reading without that cost.

- **Replace** "Prefer five narrow PRs to one large PR." with: One task, one PR, made of small ordered commits that each build and pass the tests. Stack PRs only when the owner asks for a stack.
