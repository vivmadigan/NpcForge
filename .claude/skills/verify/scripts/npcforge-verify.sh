#!/usr/bin/env bash
# Drives the NpcForge console without paying for a model call. SKILL.md says which subcommand to
# use when. Run it from Git Bash, Linux or macOS. PowerShell users take the recipe in SKILL.md.
#
#   npcforge-verify.sh start
#   npcforge-verify.sh dir
#   npcforge-verify.sh doctor
#   npcforge-verify.sh replay  [--split] [--save FILE|--temp-save] NAME [-- extra console flags]
#   npcforge-verify.sh load    [--save FILE] N
#   npcforge-verify.sh run     [--split] [--save FILE|--temp-save] NAME -- console flags
#   npcforge-verify.sh test    [FILTER]
#   npcforge-verify.sh fixture-flags
#   npcforge-verify.sh cleanup
set -u -o pipefail

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
repo=$(git -C "$here" rev-parse --show-toplevel) || exit 64
cd "$repo" || exit 64

# Exit 64 is this script's own refusal. The console's failures use other codes (127 for an
# unhandled exception under Git Bash, 124 for the watchdog), so the two never read as one.
die() { printf 'verify: %s\n' "$*" >&2; exit 64; }

# The console and the server are native Windows programs, so a path handed to them has to be in
# C:/ form. cygpath does that under Git Bash and is absent elsewhere.
native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

root="${NPCFORGE_VERIFY_ROOT:-$(native "${TEMP:-${TMPDIR:-/tmp}}")/npcforge-verify}"
fixture="$repo/NpcForge.Tests/Fixtures/replay.jsonl"
tracked_save="NpcForge.Server/characters.json"

# Each Bash call is a new shell, so the run directory travels in a file. The file is per checkout,
# because worktrees and clones of this repo share one temp folder. Two sessions verifying in the
# same checkout must set NPCFORGE_VERIFY_DIR on every call, or they share it.
latest="$root/latest-$(printf '%s' "$repo" | cksum | cut -d' ' -f1)"

rundir() {
  local dir="${NPCFORGE_VERIFY_DIR:-}"
  if [ -z "$dir" ] && [ -f "$latest" ]; then dir=$(tr -d '\r' < "$latest"); fi
  { [ -n "$dir" ] && [ -d "$dir" ]; } || die "no run directory. Run: start"
  printf '%s' "$dir"
}

# Line 1 of the fixture is the answers the run was recorded with, as a JSON array of strings.
# ReplayTests reads it the same way, so a re-record changes the replay with no edit here. Strings
# with JSON escapes are not handled; the check below stops instead of passing a wrong flag.
fixture_flags() {
  [ -f "$fixture" ] || die "fixture missing: $fixture"
  local line
  line=$(head -1 "$fixture" | tr -d '\r')
  case $line in *\\*) die "line 1 of the fixture has JSON escapes; read the flags by hand" ;; esac
  case $line in '["'*'"]') ;; *) die "line 1 of the fixture is not a JSON array of strings" ;; esac
  line=${line#'["'}
  line=${line%'"]'}
  printf '%s\n' "${line//\",\"/$'\n'}"
}

# Every run must be unable to reach a paid model, by this script's rules and not by the caller's
# care. The dead proxy in cmd_run is the second fence behind these.
guard() {
  local dir=$1; shift
  local args=("$@") i value replay=0
  for ((i = 0; i < ${#args[@]}; i++)); do
    case ${args[i]} in
      --provider)
        # The console takes the last --provider it sees, so every one has to be replay.
        [ "${args[i+1]:-}" = replay ] || die "refused: --provider ${args[i+1]:-<none>} could reach a paid model"
        replay=1 ;;
      --record)
        # A broken guard on --record plus replay would overwrite the recording it names, and the
        # error message suggests the fixture itself. Only a path inside the run directory is safe.
        value=${args[i+1]:-}
        if [ -n "$value" ] && [[ "$value" != "$dir"/* ]]; then
          die "refused: --record must name a file inside $dir, never the fixture"
        fi ;;
    esac
  done
  # Without replay, the only free run is one the roll rejects before the model's first turn. The
  # difficulty has to come first so a flag given last cannot read the next flag as its value.
  if [ $replay -eq 0 ] && ! { [ "${args[0]:-}" = --difficulty ] && [ "${args[1]:-}" = Nonsense ]; }; then
    die "refused: use --provider replay, or put --difficulty Nonsense first"
  fi
}

cmd_start() {
  local dir
  dir="$root/$(date -u +%Y%m%dT%H%M%SZ)-$$"
  mkdir -p "$dir" || die "cannot create $dir"
  printf '%s\n' "$dir" > "$latest"
  printf '%s\n' "$dir"
}

# Process names of ours, read-only. Whether a hit is ours cannot be told from here.
our_processes() {
  if command -v tasklist >/dev/null 2>&1; then
    tasklist //NH 2>/dev/null | tr -d '\r' | grep -iE '^NpcForge\.(Console|Server)\.exe' || true
  else
    pgrep -fl 'NpcForge\.(Console|Server)' || true
  fi
}

cmd_doctor() {
  local bad=0 p dll newer found v dir
  ok()   { printf 'ok:   %s\n' "$*"; }
  fail() { printf 'FAIL: %s\n' "$*"; bad=1; }

  if v=$(dotnet --version 2>/dev/null); then ok "dotnet $v"; else fail "dotnet not found"; fi

  # --no-build runs whatever is in bin/Debug, so a source newer than its dll means the run would
  # drive an old build.
  for p in Console Server Tests; do
    dll="NpcForge.$p/bin/Debug/net10.0/NpcForge.$p.dll"
    if [ ! -f "$dll" ]; then fail "not built: $dll. Run dotnet build"; continue; fi
    newer=$(find "NpcForge.$p" -type f \( -name '*.cs' -o -name '*.csproj' \) \
      -not -path '*/bin/*' -not -path '*/obj/*' -newer "$dll" | head -3)
    if [ -n "$newer" ]; then fail "stale build, newer than $dll: $(echo $newer). Run dotnet build"
    else ok "NpcForge.$p built after its last source edit"; fi
  done

  # SKILL.md is copied to bin at build, and the model is told the bin copy.
  if cmp -s NpcForge.Console/skills/npc-writer/SKILL.md \
            NpcForge.Console/bin/Debug/net10.0/skills/npc-writer/SKILL.md; then
    ok "npc-writer SKILL.md in bin matches the source"
  else
    fail "npc-writer SKILL.md in bin differs from the source. Run dotnet build"
  fi

  # A subshell, so the die inside fixture_flags fails this check and does not end the doctor.
  if (fixture_flags >/dev/null) 2>/dev/null; then
    ok "fixture holds $(($(grep -c '' "$fixture") - 1)) model calls; line 1 flags: $(fixture_flags | sed "s/.*/'&'/" | tr '\n' ' ')"
  else
    fail "fixture unreadable or line 1 not a plain JSON array: $fixture"
  fi

  # A running console or server locks NpcForge.Console.dll, so the build fails. It is also not
  # ours to kill, since the owner may have it paused in the debugger.
  found=$(our_processes)
  if [ -n "$found" ]; then fail "NpcForge processes already running (not started by this run, do not kill):"; echo "$found"
  else ok "no NpcForge.Console or NpcForge.Server process running"; fi

  if [ -z "$(git status --porcelain -- "$tracked_save")" ]; then ok "$tracked_save unmodified"
  else fail "$tracked_save is modified. A run saved to the tracked file"; fi

  [ -z "${NPCFORGE_CHARACTERS:-}" ] || printf 'warn: NPCFORGE_CHARACTERS is set in this shell (%s). This script overrides it per run, but a hand-typed run would save there\n' "$NPCFORGE_CHARACTERS"

  if dir=$(rundir 2>/dev/null); then ok "run directory: $dir"; else printf 'info: no run directory yet. Run: start\n'; fi
  return $bad
}

cmd_run() {
  local split=0 save=save.json name code temp=0
  while [ $# -gt 0 ]; do
    case $1 in
      --split) split=1; shift ;;
      --save)  save=${2:?--save needs a file name}; shift 2 ;;
      # Names no save file, as a replay run by hand does: the app picks its own temp file.
      --temp-save) temp=1; shift ;;
      *) break ;;
    esac
  done
  [ $# -ge 2 ] && [ "$2" = -- ] || die "usage: run [--split] [--save FILE|--temp-save] NAME -- console flags"
  name=$1; shift 2

  local dir savepath
  dir=$(rundir) || exit 64
  savepath="$dir/$save"
  guard "$dir" "$@"

  # HTTPS_PROXY and HTTP_PROXY point at the discard port. A run that ever lost replay fails at the
  # proxy instead of paying, as in ReplayTests. The caller's NO_PROXY is removed so it cannot
  # route around the fence. NPCFORGE_CHARACTERS is set for this child only, never exported to the
  # caller's shell, where it would redirect later hand-typed runs.
  # With --temp-save the variable is removed instead, so the app chooses the file itself.
  local savearg=(NPCFORGE_CHARACTERS="$savepath")
  [ $temp -eq 0 ] || savearg=(-u NPCFORGE_CHARACTERS)
  local child=(env -u NO_PROXY -u no_proxy "${savearg[@]}"
    MSYS_NO_PATHCONV=1
    HTTPS_PROXY=http://127.0.0.1:9 HTTP_PROXY=http://127.0.0.1:9
    https_proxy=http://127.0.0.1:9 http_proxy=http://127.0.0.1:9
    DOTNET_CLI_TELEMETRY_OPTOUT=1
    dotnet run --project "$repo/NpcForge.Console" --no-build --no-launch-profile -- "$@")
  # The watchdog ends a hung run and its children without naming a process. Windows ships its own
  # timeout.exe, which waits instead; only GNU timeout takes these arguments.
  if timeout --version 2>/dev/null | grep -q GNU; then
    child=(timeout --kill-after=5 "${NPCFORGE_VERIFY_TIMEOUT:-180}" "${child[@]}")
  fi

  { printf '%q ' "${child[@]}"; printf '\n'; } > "$dir/$name.cmd"
  printf '[verify] %s: dotnet run ... --' "$name"; printf " '%s'" "$@"; printf '\n'

  if [ $split -eq 1 ]; then
    "${child[@]}" >"$dir/$name.stdout" 2>"$dir/$name.stderr"; code=$?
    grep -E '^(\[app\]|Unhandled exception)' "$dir/$name.stderr" | cut -c1-300
    printf '[verify] stdout %s bytes, stderr %s bytes\n' \
      "$(wc -c < "$dir/$name.stdout")" "$(wc -c < "$dir/$name.stderr")"
  else
    # stdout and stderr share one pipe, so the order they were written in survives. The exit code
    # is the first stage of the pipe, not tee's.
    "${child[@]}" 2>&1 | tee "$dir/$name.txt"; code=${PIPESTATUS[0]}
  fi

  printf '%s\n' "$code" > "$dir/$name.exit"
  # A save file that exists proves the run got as far as saving, which a clean tracked
  # characters.json alone does not.
  local saved=absent out="$dir/$name.txt"
  [ $split -eq 0 ] || out="$dir/$name.stderr"
  if [ $temp -eq 1 ]; then
    # The path comes from the app's own "saved as N in <path>" line, which is what the owner reads.
    savepath=$(sed -n 's/^\[app\] saved as [0-9]* in \(.*[^[:space:]]\)[[:space:]]*$/\1/p' "$out" | tail -1)
    save="temp save ${savepath:-none printed}"
  fi
  [ -n "$savepath" ] && [ -s "$savepath" ] && saved="present ($(wc -c < "$savepath") bytes)"
  printf '%s\t%s\t%s\t%s\n' "$name" "$code" "$save $saved" "$(date -u +%H:%M:%SZ)" >> "$dir/index.tsv"
  printf '[verify] %s exit=%s; %s: %s\n' "$name" "$code" "$save" "$saved"
  return "$code"
}

cmd_replay() {
  local pre=() flags=() line text
  while [ $# -gt 0 ]; do
    case $1 in
      --split) pre+=("$1"); shift ;;
      --save)  pre+=("$1" "${2:?--save needs a file name}"); shift 2 ;;
      --temp-save) pre+=("$1"); shift ;;
      *) break ;;
    esac
  done
  [ $# -ge 1 ] || die "usage: replay [--split] [--save FILE|--temp-save] NAME [-- extra console flags]"
  local name=$1; shift
  [ $# -eq 0 ] || { [ "$1" = -- ] || die "extra flags go after --"; shift; }
  # Read into a variable first. A die inside a process substitution would leave flags empty and
  # the replay would run with random dice.
  text=$(fixture_flags) || exit 64
  while IFS= read -r line; do flags+=("$line"); done <<< "$text"
  # Flags after the fixture's win, because the console keeps the last value of a repeated flag.
  cmd_run "${pre[@]+"${pre[@]}"}" "$name" -- --provider replay --recording "$fixture" "${flags[@]}" "$@"
}

# --provider replay with --load builds the replay client, not a real one, so no API key is needed.
cmd_load() {
  local pre=()
  while [ $# -gt 0 ]; do
    case $1 in
      --save) pre+=("$1" "${2:?--save needs a file name}"); shift 2 ;;
      *) break ;;
    esac
  done
  [ $# -eq 1 ] || die "usage: load [--save FILE] N"
  # Always split, so stdout is the character exactly as saved, with no trace lines in it.
  cmd_run --split "${pre[@]+"${pre[@]}"}" "load-$1" -- --provider replay --recording "$fixture" --load "$1"
}

# dotnet test is free, with no network and no model. ReplayTests puts its own dead proxy on the child
# it starts. Build first; --no-build runs the last build.
cmd_test() {
  local dir name=test code
  local cmd=(dotnet test --no-build)
  dir=$(rundir) || exit 64
  if [ -n "${1:-}" ]; then cmd+=(--filter "FullyQualifiedName~$1"); name="test-$1"; fi
  printf '[verify] %s: %s\n' "$name" "${cmd[*]}"
  "${cmd[@]}" 2>&1 | tee "$dir/$name.txt"; code=${PIPESTATUS[0]}
  # A filter that matches nothing exits 0 with only a "No test matches" line. That is not a pass.
  if [ "$code" -eq 0 ] && ! grep -qE '^(Passed|Failed)!' "$dir/$name.txt"; then
    echo "[verify] no test ran. The filter matched nothing, or the run printed no summary"
    code=65
  fi
  printf '%s\n' "$code" > "$dir/$name.exit"
  printf '[verify] %s exit=%s\n' "$name" "$code"
  return "$code"
}

cmd_cleanup() {
  local found dir
  # Each run ends its own processes and the watchdog reaps a hung one, so there is nothing of ours
  # to stop. A process listed here was not started by this script, so leave it.
  found=$(our_processes)
  if [ -n "$found" ]; then printf 'FAIL: NpcForge processes still running (not killed by this script):\n%s\n' "$found"
  else echo "ok:   no NpcForge.Console or NpcForge.Server process running"; fi

  if [ -z "$(git status --porcelain -- "$tracked_save")" ]; then echo "ok:   $tracked_save unmodified"
  else echo "FAIL: $tracked_save is modified"; fi

  echo "tracked changes in the repo (a break-it edit must not be left here):"
  git status --short
  echo "[end of git status]"

  if dir=$(rundir 2>/dev/null); then echo "evidence kept in $dir:"; ls -l "$dir"; fi
}

sub=${1:-}; [ $# -eq 0 ] || shift
case $sub in
  start)         cmd_start "$@" ;;
  dir)           rundir; echo ;;
  doctor)        cmd_doctor "$@" ;;
  run)           cmd_run "$@" ;;
  replay)        cmd_replay "$@" ;;
  load)          cmd_load "$@" ;;
  test)          cmd_test "$@" ;;
  fixture-flags) fixture_flags ;;
  cleanup)       cmd_cleanup ;;
  *)             sed -n '2,13p' "$here/npcforge-verify.sh"; exit 64 ;;
esac
