#!/usr/bin/env bash
# Drives NpcForge's console, API and pages without paying for a model call. SKILL.md says which
# subcommand to use when. The console subcommands run from Git Bash, Linux or macOS. `api` and
# `apphost` need Git Bash on Windows. PowerShell users take the recipe in SKILL.md.
#
#   npcforge-verify.sh start
#   npcforge-verify.sh dir
#   npcforge-verify.sh doctor
#   npcforge-verify.sh replay  [--split] [--save FILE|--temp-save] [--answer TEXT] NAME [-- extra console flags]
#   npcforge-verify.sh load    [--save FILE] N
#   npcforge-verify.sh run     [--split] [--save FILE|--temp-save] [--answer TEXT] NAME -- console flags
#   npcforge-verify.sh test    [FILTER]
#   npcforge-verify.sh api     start [--seed N] | stop
#   npcforge-verify.sh apphost start [--seed N] | stop
#   npcforge-verify.sh fixture-flags
#   npcforge-verify.sh cleanup
set -u -o pipefail

here=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
repo=$(git -C "$here" rev-parse --show-toplevel) || exit 64
cd "$repo" || exit 64

# Exit 64 is this script's own refusal. The console's failures use other codes (127 for an
# unhandled exception under Git Bash, 124 for the watchdog), so the two never read as one.
die() { printf 'verify: %s\n' "$*" >&2; exit 64; }

# The console is a native Windows program, so a path handed to it has to be in
# C:/ form. cygpath does that under Git Bash and is absent elsewhere.
native() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

root="${NPCFORGE_VERIFY_ROOT:-$(native "${TEMP:-${TMPDIR:-/tmp}}")/npcforge-verify}"
fixture="$repo/NpcForge.Tests/Fixtures/replay.jsonl"
tracked_save="NpcForge.Console/characters.json"

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

# The servers a run directory started and never confirmed stopped: stop deletes the pid file only
# once nothing of the tree is left, so a file that is still there is a server to stop.
recorded_servers() {
  local kind
  for kind in api apphost; do [ ! -f "$1/$kind.pid" ] || printf '%s ' "$kind"; done
}

cmd_start() {
  local dir old left
  # A new run directory hides the old one's pid files from stop, which looks in the newest. Say so
  # here, while the old directory is still named, so a server is never left running unseen.
  if [ -f "$latest" ] && old=$(tr -d '\r' < "$latest") && [ -d "$old" ]; then
    left=$(recorded_servers "$old")
    [ -z "$left" ] || printf 'warn: %sstill recorded in %s. Stop with: NPCFORGE_VERIFY_DIR=%s %s <api|apphost> stop\n' \
      "$left" "$old" "$old" "${BASH_SOURCE[0]}" >&2
  fi
  dir="$root/$(date -u +%Y%m%dT%H%M%SZ)-$$"
  mkdir -p "$dir" || die "cannot create $dir"
  printf '%s\n' "$dir" > "$latest"
  printf '%s\n' "$dir"
}

# Process names of ours, read-only. Whether a hit is ours cannot be told from here.
our_processes() {
  if command -v tasklist >/dev/null 2>&1; then
    tasklist //NH 2>/dev/null | tr -d '\r' | grep -iE '^NpcForge\.Console\.exe' || true
  else
    pgrep -fl 'NpcForge\.Console' || true
  fi
}

cmd_doctor() {
  local bad=0 p dll newer found v dir
  ok()   { printf 'ok:   %s\n' "$*"; }
  fail() { printf 'FAIL: %s\n' "$*"; bad=1; }

  if v=$(dotnet --version 2>/dev/null); then ok "dotnet $v"; else fail "dotnet not found"; fi

  # --no-build runs whatever is in bin/Debug, so a source newer than its dll means the run would
  # drive an old build. The API and the AppHost are here because `api start` and `apphost start`
  # run them with --no-build.
  for p in Core Adapters Console Api AppHost Tests; do
    dll="NpcForge.$p/bin/Debug/net10.0/NpcForge.$p.dll"
    if [ ! -f "$dll" ]; then fail "not built: $dll. Run dotnet build"; continue; fi
    newer=$(find "NpcForge.$p" -type f \( -name '*.cs' -o -name '*.csproj' \) \
      -not -path '*/bin/*' -not -path '*/obj/*' -newer "$dll" | head -3)
    if [ -n "$newer" ]; then fail "stale build, newer than $dll: $(echo $newer). Run dotnet build"
    else ok "NpcForge.$p built after its last source edit"; fi
  done

  # Each SKILL.md is copied to bin at build, and the model is told the bin copy. The API reads its own.
  local skill app
  for app in Console Api; do
    for skill in npc-writer scene-interviewer; do
      if cmp -s "NpcForge.Core/skills/$skill/SKILL.md" \
                "NpcForge.$app/bin/Debug/net10.0/skills/$skill/SKILL.md"; then
        ok "$skill SKILL.md in $app bin matches the source"
      else
        fail "$skill SKILL.md in $app bin differs from the source. Run dotnet build"
      fi
    done
  done

  # A subshell, so the die inside fixture_flags fails this check and does not end the doctor.
  if (fixture_flags >/dev/null) 2>/dev/null; then
    ok "fixture holds $(($(grep -c '' "$fixture") - 1)) model calls; line 1 flags: $(fixture_flags | sed "s/.*/'&'/" | tr '\n' ' ')"
  else
    fail "fixture unreadable or line 1 not a plain JSON array: $fixture"
  fi

  # A running console locks NpcForge.Console.dll, so the build fails. It is also not
  # ours to kill, since the owner may have it paused in the debugger.
  found=$(our_processes)
  if [ -n "$found" ]; then fail "NpcForge processes already running (not started by this run, do not kill):"; echo "$found"
  else ok "no NpcForge.Console process running"; fi

  if [ -z "$(git status --porcelain -- "$tracked_save")" ]; then ok "$tracked_save unmodified"
  else fail "$tracked_save is modified. A run saved to the tracked file"; fi

  [ -z "${NPCFORGE_CHARACTERS:-}" ] || printf 'warn: NPCFORGE_CHARACTERS is set in this shell (%s). This script overrides it per run, but a hand-typed run would save there\n' "$NPCFORGE_CHARACTERS"

  if dir=$(rundir 2>/dev/null); then
    ok "run directory: $dir"
    # A running API or AppHost locks its dll, so the next dotnet build fails.
    found=$(recorded_servers "$dir")
    if [ -n "$found" ]; then fail "started and not stopped: ${found% }. Run: <api|apphost> stop"
    else ok "no API or AppHost left running from this run directory"; fi
  else printf 'info: no run directory yet. Run: start\n'; fi
  return $bad
}

cmd_run() {
  local split=0 save=save.json name code temp=0 answer=
  while [ $# -gt 0 ]; do
    case $1 in
      --split) split=1; shift ;;
      --save)  save=${2:?--save needs a file name}; shift 2 ;;
      # Names no save file, as a replay run by hand does: the app picks its own temp file.
      --temp-save) temp=1; shift ;;
      # What the GM types after the character, as printf %b text such as 'save\n'. None is end of input.
      --answer) answer=${2?--answer needs text}; shift 2 ;;
      *) break ;;
    esac
  done
  [ $# -ge 2 ] && [ "$2" = -- ] || die "usage: run [--split] [--save FILE|--temp-save] [--answer TEXT] NAME -- console flags"
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

  # The console reads a file, never the caller's terminal, so the question after the character gets
  # its answer, or end of input, and not a wait for the watchdog.
  printf '%b' "$answer" > "$dir/$name.in"
  { printf '%q ' "${child[@]}"; printf '< %q\n' "$dir/$name.in"; } > "$dir/$name.cmd"
  printf '[verify] %s: dotnet run ... --' "$name"; printf " '%s'" "$@"; printf '\n'

  if [ $split -eq 1 ]; then
    "${child[@]}" <"$dir/$name.in" >"$dir/$name.stdout" 2>"$dir/$name.stderr"; code=$?
    grep -E '^(\[app\]|Unhandled exception)' "$dir/$name.stderr" | cut -c1-300
    printf '[verify] stdout %s bytes, stderr %s bytes\n' \
      "$(wc -c < "$dir/$name.stdout")" "$(wc -c < "$dir/$name.stderr")"
  else
    # stdout and stderr share one pipe, so the order they were written in survives. The exit code
    # is the first stage of the pipe, not tee's.
    "${child[@]}" <"$dir/$name.in" 2>&1 | tee "$dir/$name.txt"; code=${PIPESTATUS[0]}
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
  # A replay answers save, as a GM would to keep the character. A later --answer replaces it, and
  # --answer '' is end of input (ADR-013, decision 2).
  local pre=(--answer 'save\n') flags=() line text
  while [ $# -gt 0 ]; do
    case $1 in
      --split) pre+=("$1"); shift ;;
      --save)  pre+=("$1" "${2:?--save needs a file name}"); shift 2 ;;
      --temp-save) pre+=("$1"); shift ;;
      --answer) pre+=("$1" "${2?--answer needs text}"); shift 2 ;;
      *) break ;;
    esac
  done
  [ $# -ge 1 ] || die "usage: replay [--split] [--save FILE|--temp-save] [--answer TEXT] NAME [-- extra console flags]"
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

# Servers. `api` is NpcForge.Api alone, on a port of its own. `apphost` is the AppHost, which starts
# the API and the pages on the ports its launch profiles fix. A console run ends by itself and a
# server does not, so stop is half the job. Start records the pid, and every process the tree has
# held with its start time (process-tree.ps1). Stop ends only what those lines name, so it never
# ends a process it did not start, and then checks that none of them, and no listener, is left.

# The port a launch profile fixes: the first "KEY": "http://localhost:PORT" in the file.
launch_port() {
  local port
  port=$(sed -n "s|.*\"$2\": *\"http://localhost:\([0-9]*\)\".*|\1|p" "$1" | head -1)
  [ -n "$port" ] || die "no $2 port in $1"
  printf '%s' "$port"
}

# "PORT PID" for every TCP listener, IPv4 or IPv6. Plain netstat lists both; with -p tcp it lists
# IPv4 only, which hides a Vite on [::1]:5173 and a held port with it.
tcp_listeners() {
  netstat -ano | tr -d '\r' | awk '$1 == "TCP" && $4 == "LISTENING" { n = split($2, a, ":"); print a[n], $5 }'
}

# The lines of tcp_listeners for the given ports.
listeners() { tcp_listeners | awk -v ports=" $* " 'index(ports, " " $1 " ")'; }

# The first port from 5400 that nothing listens on. Never 5173, 5180, 5381 or 5382, which are the
# pages, the API's launch profile and the browser tests' two (lesson 018).
free_port() {
  local held p
  held=$(tcp_listeners | cut -d' ' -f1)
  for p in $(seq 5400 5499); do
    grep -qx "$p" <<< "$held" || { printf '%s' "$p"; return 0; }
  done
  return 1
}

pid_name() { tasklist //NH //FI "PID eq $1" 2>/dev/null | tr -d '\r' | awk 'NF && $1 != "INFO:" { print $1; exit }'; }

# Live members of a server's tree as "pid<TAB>start<TAB>name": the pids in ROOTS, then everything
# the file KNOWN names or descends from it.
tree_members() {
  VERIFY_ROOTS=$1 VERIFY_KNOWN=$(cygpath -w "$2") MSYS_NO_PATHCONV=1 \
    powershell.exe -NoProfile -NonInteractive \
      -Command "Get-Content -Raw '$(cygpath -w "$here/process-tree.ps1")' | Invoke-Expression" | tr -d '\r'
}

# Adds the live members to KNOWN, which keeps the dead ones too: a child whose parent died is found
# through the parent's pid and start time. Prints the live members.
remember() {
  local found
  found=$(tree_members "$2" "$1") || return 1
  { [ ! -f "$1" ] || cat "$1"; printf '%s\n' "$found"; } | sed '/^$/d' | sort -u > "$1.new" && mv "$1.new" "$1"
  printf '%s' "$found"
}

members_line() { awk -F'\t' 'NF { printf "%s%s %s", sep, $1, $3; sep = ", " } END { if (!sep) printf "none" }' <<< "$1"; }
has_pid()      { awk -F'\t' -v p="$2" '$1 == p { f = 1 } END { exit !f }' <<< "$1"; }

server_start() {
  local kind=$1; shift
  local seed= provider=stand-in
  while [ $# -gt 0 ]; do
    case $1 in
      --seed) seed=${2:?--seed needs a number}; shift 2 ;;
      # The stand-in is the one free model. The flag is here so that a paid one is refused by name.
      --provider) provider=${2:?--provider needs a name}; shift 2 ;;
      *) die "usage: $kind start [--seed N]" ;;
    esac
  done
  [ "$provider" = stand-in ] || die "refused: --provider $provider could reach a paid model. The $kind runs on the stand-in only"
  case $seed in *[!0-9]*) die "--seed needs a whole number" ;; esac
  server_platform

  local dir ports port key p held hpid
  dir=$(rundir) || exit 64
  [ ! -f "$dir/$kind.pid" ] || die "$kind already started in $dir (pid $(tr -d '\r' < "$dir/$kind.pid")). Run: $kind stop"

  if [ "$kind" = api ]; then
    port=$(free_port) || die "no free port from 5400 to 5499"
    ports=$port
  else
    # The AppHost's fixed ports: the API's from its launch profile, then the dashboard's, its
    # telemetry's and its resource service's from the AppHost's. The pages' own port is picked by Aspire.
    for key in NpcForge.Api:applicationUrl NpcForge.AppHost:applicationUrl \
               NpcForge.AppHost:ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL NpcForge.AppHost:ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL; do
      p=$(launch_port "${key%%:*}/Properties/launchSettings.json" "${key#*:}") || exit 64
      ports="${ports:+$ports }$p"
    done
    port=${ports%% *}
  fi
  # A server on one of these ports is not ours to stop, and a test run against the port would read
  # its answer, not ours (lesson 018).
  held=$(listeners $ports | head -1)
  if [ -n "$held" ]; then
    read -r p hpid <<< "$held"
    die "refused: port $p is held by pid $hpid ($(pid_name "$hpid")), which this run did not start. Another session's server, or the owner's. It is left alone: stop it yourself, or wait"
  fi

  # The made-up saves, the file the browser tests' API starts from (playwright.config.ts), so the
  # pages show the states their screenshots show. A copy in the run directory, never the owner's
  # characters.json. Rows let a listing be compared with this file and nothing else: an empty start
  # could not tell this file from a missing one.
  cp NpcForge.Web/src/madeUp/saves.json "$dir/$kind.saves.json" || die "cannot copy the made-up saves"

  # The fences. The stand-in answers every model call, and NPCFORGE_OFFLINE refuses a paid provider
  # if the stand-in is ever lost, as playwright.config.ts does. NPCFORGE_CHARACTERS is a full
  # Windows path, which the API requires and the AppHost passes on. The API alone runs with
  # --no-launch-profile, so it is Production and reads no user secrets.
  local envs=(MSYS_NO_PATHCONV=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
    NPCFORGE_CHARACTERS="$(cygpath -w "$dir/$kind.saves.json")" NPCFORGE_PROVIDER=stand-in NPCFORGE_OFFLINE=1)
  [ -z "$seed" ] || envs+=(NPCFORGE_SEED="$seed")
  local cmd
  if [ "$kind" = api ]; then
    envs+=(ASPNETCORE_URLS="http://localhost:$port")
    cmd=(dotnet run --project "$repo/NpcForge.Api" --no-build --no-launch-profile)
  else
    cmd=(dotnet run --project "$repo/NpcForge.AppHost" --no-build)
  fi

  local log="$dir/$kind.log" job win i first
  { printf '%q ' env "${envs[@]}" "${cmd[@]}"; printf '\n'; } > "$dir/$kind.cmd"
  # In the background with its own log and no terminal, so the call returns and nothing waits on it.
  env "${envs[@]}" "${cmd[@]}" > "$log" 2>&1 < /dev/null &
  job=$!
  for i in $(seq 1 30); do win=$(cat "/proc/$job/winpid" 2>/dev/null) && [ -n "$win" ] && break; sleep 0.1; done
  [ -n "${win:-}" ] || { kill "$job" 2>/dev/null; die "cannot read the started process's pid"; }
  printf '%s\n' "$win" > "$dir/$kind.pid"
  printf '%s\n' "$ports" > "$dir/$kind.ports"
  # The tree's first record, taken while the process is only just up, so stop can tell it from a
  # process that later reuses its pid. A root that is not dotnet is the wrong process: never record it.
  first=$(remember "$dir/$kind.tree" "$win")
  if ! grep -q "^$win	[0-9]*	dotnet.exe$" <<< "$first"; then
    kill "$job" 2>/dev/null; rm -f "$dir/$kind.pid"
    die "pid $win is not the dotnet process this run started. Nothing was recorded or killed"
  fi
  printf '[verify] %s: started pid %s, log %s\n' "$kind" "$win" "$log"

  # A refused connection takes about two seconds on Windows, so 20 tries are up to a minute. curl
  # with --max-time, not Invoke-RestMethod, which waited 368 s for a server curl reached in 14.
  local url="http://localhost:$port" code=000 tries=0
  while [ $tries -lt 20 ]; do
    tries=$((tries + 1))
    kill -0 "$job" 2>/dev/null || break
    code=$(curl -s --max-time 2 -o /dev/null -w '%{http_code}' "$url/api/saves") || code=000
    [ "$code" = 200 ] && break
    sleep 1
  done
  if [ "$code" != 200 ]; then
    printf 'FAIL: %s answered %s from %s/api/saves after %s tries. End of %s:\n' "$kind" "$code" "$url" "$tries" "$log"
    tail -n 20 "$log"
    server_stop "$kind" || true
    exit 1
  fi
  printf '%s\n' "$url" > "$dir/$kind.url"
  printf '[verify] %s ready after %s tries: %s (saves: %s)\n' "$kind" "$tries" "$url" "$dir/$kind.saves.json"

  # The pages start once the API is healthy, on a port Aspire picks and Vite binds on [::1] alone.
  # Find it as the port the tree's node process listens on, and ask it for /api/saves: Vite
  # forwards that to the API, so the answer proves the pages reach the API too.
  if [ "$kind" = apphost ]; then
    local nodes pages_url= n=0
    while [ $n -lt 20 ] && [ -z "$pages_url" ]; do
      n=$((n + 1))
      nodes=$(remember "$dir/$kind.tree" "" | awk -F'\t' '$3 == "node.exe" { printf "%s ", $1 }')
      for p in $(tcp_listeners | awk -v pids=" $nodes" 'index(pids, " " $2 " ") { print $1 }'); do
        [ "$(curl -s --max-time 2 -o /dev/null -w '%{http_code}' "http://localhost:$p/api/saves")" = 200 ] && { pages_url="http://localhost:$p"; break; }
      done
      [ -n "$pages_url" ] || sleep 1
    done
    if [ -z "$pages_url" ]; then
      printf 'FAIL: no node process of the tree answered /api/saves after %s tries. A first start in a fresh checkout installs the page packages first: run npm --prefix NpcForge.Web install once, then start again. End of %s:\n' "$n" "$log"
      tail -n 20 "$log"
      server_stop "$kind" || true
      exit 1
    fi
    printf '%s\n' "$pages_url" > "$dir/$kind.pages"
    printf '%s %s\n' "$ports" "${pages_url##*:}" > "$dir/$kind.ports"
    printf '[verify] %s pages ready after %s tries: %s (the dashboard is http://localhost:%s)\n' "$kind" "$n" "$pages_url" "$(cut -d' ' -f2 <<< "$ports")"
  fi
}

server_stop() {
  local kind=$1 dir root known ports before left held i p pid why
  dir=$(rundir) || exit 64
  [ -f "$dir/$kind.pid" ] || { printf 'ok:   no %s recorded in %s, so nothing to stop\n' "$kind" "$dir"; return 0; }
  root=$(tr -d '\r' < "$dir/$kind.pid")
  known="$dir/$kind.tree"
  ports=$(cat "$dir/$kind.ports")
  [ -s "$known" ] || die "no $known, so pid $root cannot be told from a process that reused it. Nothing was killed. Look at pid $root by hand"

  before=$(remember "$known" "")
  printf '[verify] %s stop: running before: %s\n' "$kind" "$(members_line "$before")"
  # The whole tree of the root. For the AppHost that is the AppHost alone: it starts DCP detached,
  # and DCP ends the dashboard, the API and Vite itself once it sees the AppHost gone.
  if has_pid "$before" "$root"; then
    printf '[verify] taskkill //PID %s //T //F\n' "$root"
    taskkill //PID "$root" //T //F > /dev/null 2>&1
  fi
  for i in $(seq 1 10); do [ -z "$(listeners $ports)" ] && break; sleep 1; done

  # Whatever is left is on the recorded tree by pid and start time, so it is ours to end by pid.
  left=$(remember "$known" "")
  if [ -n "$left" ]; then
    printf '[verify] still running after the tree kill: %s. Ending each by pid\n' "$(members_line "$left")"
    while IFS=$'\t' read -r p _; do taskkill //PID "$p" //T //F > /dev/null 2>&1; done <<< "$left"
    sleep 1
    left=$(remember "$known" "")
  fi
  held=$(listeners $ports)

  if [ -z "$left" ] && [ -z "$held" ]; then
    rm -f "$dir/$kind.pid"
    printf 'ok:   %s stopped. Running after: none. Nothing listens on %s\n' "$kind" "$ports"
    return 0
  fi
  [ -z "$left" ] || printf 'FAIL: still running from the %s tree: %s\n' "$kind" "$(members_line "$left")"
  # A holder outside the recorded tree is not ours, so it is reported and never ended.
  while read -r p pid; do
    [ -n "${p:-}" ] || continue
    if has_pid "$left" "$pid"; then why="on the recorded tree, and it would not end"
    else why="not on the recorded tree, so this script leaves it"; fi
    printf 'FAIL: port %s is held by pid %s (%s), %s\n' "$p" "$pid" "$(pid_name "$pid")" "$why"
  done <<< "$(sort -u <<< "$held")"
  return 1
}

# Everything an api or apphost call needs from the machine is Windows's: taskkill ends the tree,
# cygpath gives the saves file its full Windows path, powershell.exe lists the tree.
server_platform() {
  local tool
  for tool in taskkill cygpath powershell.exe curl; do
    command -v "$tool" > /dev/null 2>&1 || die "api and apphost need Git Bash on Windows, with $tool on PATH"
  done
}

cmd_server() {
  local kind=$1 action=${2:-}
  [ $# -eq 0 ] || shift; [ $# -eq 0 ] || shift
  case $action in
    start) server_start "$kind" "$@" ;;
    stop)  [ $# -eq 0 ] || die "usage: $kind stop"; server_platform; server_stop "$kind" ;;
    *)     die "usage: $kind start [--seed N] | $kind stop" ;;
  esac
}

cmd_cleanup() {
  local found dir
  # Each run ends its own processes and the watchdog reaps a hung one, so there is nothing of ours
  # to stop. A process listed here was not started by this script, so leave it.
  found=$(our_processes)
  if [ -n "$found" ]; then printf 'FAIL: NpcForge processes still running (not killed by this script):\n%s\n' "$found"
  else echo "ok:   no NpcForge.Console process running"; fi

  if [ -z "$(git status --porcelain -- "$tracked_save")" ]; then echo "ok:   $tracked_save unmodified"
  else echo "FAIL: $tracked_save is modified"; fi

  echo "tracked changes in the repo (a break-it edit must not be left here):"
  git status --short
  echo "[end of git status]"

  if dir=$(rundir 2>/dev/null); then
    found=$(recorded_servers "$dir")
    if [ -n "$found" ]; then echo "FAIL: started and not stopped: ${found% }. Run: <api|apphost> stop"
    else echo "ok:   no API or AppHost left running from this run directory"; fi
    echo "evidence kept in $dir:"; ls -l "$dir"
  fi
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
  api|apphost)   cmd_server "$sub" "$@" ;;
  fixture-flags) fixture_flags ;;
  cleanup)       cmd_cleanup ;;
  *)             sed -n '2,16p' "$here/npcforge-verify.sh"; exit 64 ;;
esac
