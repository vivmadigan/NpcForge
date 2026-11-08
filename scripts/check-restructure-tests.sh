#!/usr/bin/env bash
# Proves a restructure PR changed no test's behaviour (ADR-014 decision 9; ROADMAP.md epic 5, "Done when":
# "a script shows that every test-file change is a `using` line or a project reference"). A restructure
# moves code between projects while the tests stay as they are, so a test edit that is not about where a
# type lives is a behaviour change smuggled into a restructure. The tests' namespace sits inside NpcForge,
# so a moved type needs no new `using`, and most restructure PRs change no test at all.
#
# Over the diff from the merge base of BASE and HEAD, in NpcForge.Tests/, it fails unless:
#   - nothing under NpcForge.Tests/Fixtures/ differs from base (the recordings and goldens stay byte-identical);
#   - every changed line in an existing file is a `using` directive, a ProjectReference line, a blank line,
#     or a line that equals a removed line once `NpcForge.Server.` and `NpcForge.Core.` qualifiers are stripped
#     (DiceOrderTests names `NpcForge.Server.CharacterTools.ReadCast` in full);
#   - no existing test file is deleted.
# A new file is allowed, since it changes no existing test, and is listed for review.
#
# Usage: scripts/check-restructure-tests.sh [BASE]     BASE defaults to origin/master. Run from Git Bash.
set -u

base="${1:-origin/master}"
cd "$(git rev-parse --show-toplevel)" || exit 2

git rev-parse --verify --quiet "$base^{commit}" >/dev/null || { echo "check-restructure-tests: no such ref: $base" >&2; exit 2; }
mb=$(git merge-base "$base" HEAD) || { echo "check-restructure-tests: $base and HEAD share no history" >&2; exit 2; }

echo "base $base (merge base $(git rev-parse --short "$mb")), head $(git rev-parse --short HEAD)"

fail=0
changed=0

# What counts as a line that cannot change a test's behaviour, read from a `+` or `-` diff line with its sign removed.
# The rest are paired by their text with qualifiers stripped: every removed line needs an added twin and the reverse.
read -r -d '' pair_lines <<'AWK'
function trivial(s) {
    if (s ~ /^[[:space:]]*$/) return "blank"
    if (s ~ /^[[:space:]]*(global[[:space:]]+)?using[[:space:]]+(static[[:space:]]+)?([A-Za-z_][A-Za-z0-9_]*[[:space:]]*=[[:space:]]*)?[A-Za-z_][A-Za-z0-9_.]*(<[^;()]*>)?[[:space:]]*;[[:space:]]*(\/\/.*)?$/) return "using"
    if (s ~ /^[[:space:]]*<ProjectReference[[:space:]\/>]/) return "projref"
    return ""
}
/^@@/ { inhunk = 1; next }
!inhunk { next }
/^\\/ { next }
{
    sign = substr($0, 1, 1)
    text = substr($0, 2)
    sub(/^\357\273\277/, "", text)
    kind = trivial(text)
    if (kind != "") { count[kind]++; next }
    norm = text
    gsub(/NpcForge\.(Server|Core)\./, "", norm)
    if (sign == "+") { plus[norm]++; rawplus[norm] = text } else { minus[norm]++; rawminus[norm] = text }
    keys[norm] = 1
}
END {
    pairs = 0
    for (k in keys) {
        p = plus[k] + 0; m = minus[k] + 0
        pairs += (p < m ? p : m)
        if (p > m) print "BAD + " rawplus[k]
        if (m > p) print "BAD - " rawminus[k]
    }
    printf "SUM %d %d %d %d\n", count["using"] + 0, count["blank"] + 0, count["projref"] + 0, pairs
}
AWK

# Fixtures: byte-identical to base.
fixtures=$(git diff --no-renames --name-status "$mb" HEAD -- NpcForge.Tests/Fixtures)
if [ -n "$fixtures" ]; then
    fail=1
    echo "FAIL NpcForge.Tests/Fixtures differs from base. The replay and the goldens are never edited in a restructure:"
    echo "$fixtures" | sed 's/^/       /'
else
    echo "ok   NpcForge.Tests/Fixtures is byte-identical to base"
fi

# Every other changed file under NpcForge.Tests/.
while IFS=$'\t' read -r status path; do
    [ -z "$status" ] && continue
    changed=$((changed + 1))
    case "$status" in
        A)
            echo "ok   $path: a new test file. Allowed, since it changes no existing test. Listed so you review what it asserts."
            ;;
        M)
            result=$(git diff -U0 --no-renames --no-color "$mb" HEAD -- "$path" | tr -d '\r' | awk "$pair_lines")
            bad=$(printf '%s\n' "$result" | grep '^BAD ' || true)
            if [ -n "$bad" ]; then
                fail=1
                echo "FAIL $path: a changed line is not a using line, a ProjectReference, a blank line or a namespace qualifier:"
                printf '%s\n' "$bad" | sed 's/^BAD /       /'
            else
                set -- $(printf '%s\n' "$result" | grep '^SUM ')
                echo "ok   $path: $2 using line(s), $3 blank line(s), $4 ProjectReference line(s), $5 line(s) changed only by an NpcForge.Server. or NpcForge.Core. qualifier"
            fi
            ;;
        *)
            fail=1
            echo "FAIL $path: status $status. A restructure may add a test file but not delete or retype one."
            ;;
    esac
done < <(git diff --no-renames --name-status "$mb" HEAD -- NpcForge.Tests ':!NpcForge.Tests/Fixtures')

[ "$changed" -eq 0 ] && echo "ok   no other file under NpcForge.Tests/ changed"

if [ "$fail" -ne 0 ]; then
    echo "FAIL: this is not a restructure of the tests' behaviour (ADR-014 decision 9)."
    exit 1
fi
echo "PASS: every test-file change is a using line, a ProjectReference, a blank line or a namespace qualifier, and the fixtures are untouched."
