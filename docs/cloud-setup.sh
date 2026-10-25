#!/bin/bash
# The setup script for NpcForge's cloud environment. Paste it into the environment's Setup script
# box at claude.ai/code (docs/pstack.md "In the cloud" says how). It is kept here so a change to it
# is reviewed like code; the environment runs the pasted copy, not this file.
#
# A cloud session reads the repo's .claude/settings.json but does not install the plugins it
# declares, and nothing in ~/.claude on the owner's machine reaches it. So this script installs
# pstack into the VM's own ~/.claude and writes the two files that keep it off Fable.
# Every step ends in || true: a setup script that exits non-zero stops the session starting.

mkdir -p ~/.claude

# pstack, from the same marketplace the repo's settings name.
if command -v claude >/dev/null 2>&1; then
  claude plugin marketplace add michael-denyer/pstack-claude || true
  claude plugin install pstack@pstack-claude || true
else
  echo "cloud-setup: claude is not on PATH, so pstack was not installed" >&2
fi

# pstack's per-role models. Its defaults name Fable, which is outside the budget, so every role
# that defaults to fable is set here, as in ~/.claude/pstack-models.md on the owner's machine.
cat > ~/.claude/pstack-models.md <<'EOF' || true
# pstack model configuration

Fable is never used, by the user's choice (budget). Every role that defaults to `fable` is set to
`opus` here, and `fable` is removed from every panel. Do not delete those lines, because a missing
line falls back to a default that may be Fable.

feature, refactoring: sonnet
bug-fix: opus
perf-issue: opus
hillclimb: opus
judgment and prose: opus
strongest judgment: opus
how explorer: opus
how explainer: opus
why investigators: opus
why synthesizer: opus
reflect tooling: opus
reflect judgment, divergent, synthesizer: opus
arena runners: opus, sonnet, haiku
arena cross-judge pool: opus, sonnet, haiku
swarm workers: opus
architect runners: opus, sonnet, haiku
interrogate reviewers: opus, sonnet, haiku

default effort: session
session hook: on
EOF

# The owner's global instructions, loaded as user instructions in the session.
cat > ~/.claude/CLAUDE.md <<'EOF' || true
# Model budget

Never start an agent, review panel, arena or judge on Fable (`fable`, `claude-fable-*`), and never
suggest switching to it: it is outside my budget. Use `opus` for the hardest judgement, `sonnet` for
implementation and `haiku` for cheap checks.

pstack's per-role models, with Fable removed:

@~/.claude/pstack-models.md
EOF

exit 0
