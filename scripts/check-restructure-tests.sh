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
#     or the same line as the one it replaced once `NpcForge.Server.` and `NpcForge.Core.` are both written
#     `NpcForge.` (DiceOrderTests names `NpcForge.Server.CharacterTools.ReadCast` in full, and
#     `NpcForge.CharacterTools.ReadCast` is the same call after the move). Removed and added lines are paired
#     one to one, per hunk and in order, so two lines swapped or a line added in a hunk fails;
#   - no existing test file is deleted.
# A new file is allowed, since it changes no existing test, and is listed for review.
#
# `--allow-changed <path>` (repeatable) names an existing test file the owner has agreed to change in this PR.
# Its changes are not checked: they are printed under ALLOWED BY OWNER with a diff stat, so the reviewer reads
# them, and they do not fail the run. Name the file in the PR. Nothing else is exempt, and a path under
# NpcForge.Tests/Fixtures is refused, because the replay is never re-recorded in a restructure.
#
# Usage: scripts/check-restructure-tests.sh [--allow-changed PATH]... [BASE]
#        BASE defaults to origin/master. Run from Git Bash.
set -u

base=""
allowed=()
while [ $# -gt 0 ]; do
    case "$1" in
        --allow-changed)
            [ $# -ge 2 ] || { echo "check-restructure-tests: --allow-changed needs a path" >&2; exit 2; }
            allowed+=("$(printf '%s' "$2" | tr '\\' '/' | sed 's|^\./||')")
            shift 2
            ;;
        --allow-changed=*)
            allowed+=("$(printf '%s' "${1#--allow-changed=}" | tr '\\' '/' | sed 's|^\./||')")
            shift
            ;;
        -*)
            echo "check-restructure-tests: unknown option $1" >&2
            exit 2
            ;;
        *)
            [ -z "$base" ] || { echo "check-restructure-tests: one BASE only, got '$base' and '$1'" >&2; exit 2; }
            base="$1"
            shift
            ;;
    esac
done
base="${base:-origin/master}"
for path in ${allowed[@]+"${allowed[@]}"}; do
    case "$path" in
        NpcForge.Tests/Fixtures|NpcForge.Tests/Fixtures/*)
            echo "check-restructure-tests: $path is a fixture. The replay and the goldens are never exempt." >&2
            exit 2
            ;;
        NpcForge.Tests/*) ;;
        *)
            echo "check-restructure-tests: --allow-changed takes a file under NpcForge.Tests/, got $path" >&2
            exit 2
            ;;
    esac
done
cd "$(git rev-parse --show-toplevel)" || exit 2

git rev-parse --verify --quiet "$base^{commit}" >/dev/null || { echo "check-restructure-tests: no such ref: $base" >&2; exit 2; }
mb=$(git merge-base "$base" HEAD) || { echo "check-restructure-tests: $base and HEAD share no history" >&2; exit 2; }

echo "base $base (merge base $(git rev-parse --short "$mb")), head $(git rev-parse --short HEAD)"

fail=0
changed=0

# What counts as a line that cannot change a test's behaviour, read from a `+` or `-` diff line with its sign removed.
# The other lines are paired within their hunk, in order: the first removed line with the first added line, and so
# on. A pair passes when its two lines are equal once `NpcForge.Server.` and `NpcForge.Core.` are both written
# `NpcForge.`. A pair that differs, or a line with no partner, is reported. Pairing by position, not by a count of
# equal lines, is what makes two lines swapped inside a test fail.
read -r -d '' pair_lines <<'AWK'
function trivial(s) {
    if (s ~ /^[[:space:]]*$/) return "blank"
    if (s ~ /^[[:space:]]*(global[[:space:]]+)?using[[:space:]]+(static[[:space:]]+)?([A-Za-z_][A-Za-z0-9_]*[[:space:]]*=[[:space:]]*)?[A-Za-z_][A-Za-z0-9_.]*(<[^;()]*>)?[[:space:]]*;[[:space:]]*(\/\/.*)?$/) return "using"
    if (s ~ /^[[:space:]]*<ProjectReference[[:space:]\/>]/) return "projref"
    return ""
}
function flush(    i, k) {
    k = (nm < np ? nm : np)
    for (i = 1; i <= k; i++) {
        if (normm[i] == normp[i]) pairs++
        else { print "BAD - " rawm[i]; print "BAD + " rawp[i] }
    }
    for (i = k + 1; i <= nm; i++) print "BAD - " rawm[i]
    for (i = k + 1; i <= np; i++) print "BAD + " rawp[i]
    nm = 0; np = 0
}
/^@@/ { flush(); inhunk = 1; next }
!inhunk { next }
/^\\/ { next }
{
    sign = substr($0, 1, 1)
    text = substr($0, 2)
    sub(/^\357\273\277/, "", text)
    kind = trivial(text)
    if (kind != "") { count[kind]++; next }
    norm = text
    gsub(/NpcForge\.(Server|Core)\./, "NpcForge.", norm)
    if (sign == "+") { np++; rawp[np] = text; normp[np] = norm } else { nm++; rawm[nm] = text; normm[nm] = norm }
}
END {
    flush()
    printf "SUM %d %d %d %d\n", count["using"] + 0, count["blank"] + 0, count["projref"] + 0, pairs + 0
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

# Every other changed file under NpcForge.Tests/. A file the owner listed is set aside and printed below.
allowed_seen=()
while IFS=$'\t' read -r status path; do
    [ -z "$status" ] && continue
    changed=$((changed + 1))
    skip=0
    for listed in ${allowed[@]+"${allowed[@]}"}; do
        if [ "$listed" = "$path" ]; then
            allowed_seen+=("$status"$'\t'"$path")
            skip=1
        fi
    done
    [ "$skip" -eq 1 ] && continue
    case "$status" in
        A)
            echo "ok   $path: a new test file. Allowed, since it changes no existing test. Listed so you review what it asserts."
            ;;
        M)
            result=$(git diff -U0 --no-renames --no-color "$mb" HEAD -- "$path" | tr -d '\r' | awk "$pair_lines")
            bad=$(printf '%s\n' "$result" | grep '^BAD ' || true)
            if [ -n "$bad" ]; then
                fail=1
                echo "FAIL $path: a changed line is not a using line, a ProjectReference, a blank line, or the same line as the one it replaced in the same place once its NpcForge.Server. or NpcForge.Core. qualifier is written NpcForge.:"
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

# The owner's exceptions, loud, with what each one changed, so the reviewer reads the diff the script did not check.
for listed in ${allowed[@]+"${allowed[@]}"}; do
    seen=0
    for entry in ${allowed_seen[@]+"${allowed_seen[@]}"}; do
        [ "${entry#*$'\t'}" = "$listed" ] && seen=1
    done
    [ "$seen" -eq 0 ] && echo "note $listed was listed with --allow-changed but does not differ from base"
done
if [ "${#allowed_seen[@]}" -gt 0 ]; then
    echo
    echo "ALLOWED BY OWNER (listed in the PR): the changes below were NOT checked by this script. Read each diff."
    for entry in "${allowed_seen[@]}"; do
        echo "  ${entry%%$'\t'*}  ${entry#*$'\t'}"
        git diff --no-renames --stat=100 "$mb" HEAD -- "${entry#*$'\t'}" | sed 's/^/      /'
    done
    echo
fi

if [ "$fail" -ne 0 ]; then
    echo "FAIL: this is not a restructure of the tests' behaviour (ADR-014 decision 9)."
    exit 1
fi
echo "PASS: every test-file change is a using line, a ProjectReference, a blank line or a namespace qualifier, and the fixtures are untouched${allowed_seen:+, apart from the ${#allowed_seen[@]} file(s) the owner listed}."
