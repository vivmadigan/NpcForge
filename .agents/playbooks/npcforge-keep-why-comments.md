---
extends: opening-a-pr, feature, bug-fix, refactoring
when: Any NpcForge task that changes code, which ends by running Opening a PR.
---

# NpcForge: keep the why-comments

NpcForge's comments are why-comments. Each one names the rule a line keeps: a rule in AGENTS.md
"Rules that must not break", docs/architecture.md, an ADR or a lesson. The owner reads more code
than they write, and these comments are how they follow it. They are not slop, and they are not
noise for a reviewer to strip.

- **Replace** "Run `/deslop` over the diff before commit and `/no-comments` before review." with: Run `/deslop` over the diff before commit, and keep every comment that names a rule. Do not run `/no-comments`, and do not spawn `pstack:comment-sicko`.
