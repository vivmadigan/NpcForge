#!/usr/bin/env bash
# Bash version of protect-docs.ps1, for macOS/Linux machines without PowerShell.
# To use it, change the PreToolUse entry in hooks/hooks.json to:
#   { "type": "command", "command": "bash", "args": ["${CLAUDE_PLUGIN_ROOT}/scripts/protect-docs.sh"], "timeout": 15 }
# Requires jq.

set -u
input=$(cat)
[ -z "$input" ] && exit 0

path=$(printf '%s' "$input" | jq -r '.tool_input.file_path // empty' 2>/dev/null) || exit 0
[ -z "$path" ] && exit 0

normalised=${path//\\//}
case "$normalised" in
  */docs/decisions/*|*/docs/plans/*) ;;
  *) exit 0 ;;
esac

name=$(basename "$normalised")
[ "$(printf '%s' "$name" | tr '[:upper:]' '[:lower:]')" = "index.md" ] && exit 0
[ -f "$path" ] || exit 0   # new file: allowed

tool=$(printf '%s' "$input" | jq -r '.tool_name // empty')
new_text=$(printf '%s' "$input" | jq -r '.tool_input.new_string // empty')
if [ "$tool" = "Edit" ] && printf '%s' "$new_text" | grep -qi 'superseded by'; then
  exit 0   # the one legal edit
fi

echo "Blocked: $name is a decision record or plan version and is immutable once written. Write a new file that supersedes it (next number) and add it to INDEX.md. The only edit allowed to the old file is its status line, changed to 'Superseded by <new record>'." >&2
exit 2
