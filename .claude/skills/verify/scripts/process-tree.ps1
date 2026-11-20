# Prints the live processes that belong to a server npcforge-verify.sh started, one per line as
# "pid<TAB>start<TAB>name". The helper reads this file and runs it with Invoke-Expression, so the
# machine's execution policy never applies. Inputs are environment variables: VERIFY_ROOTS, the pids
# the helper has just started, and VERIFY_KNOWN, a file of lines this script printed earlier.
#
# A process belongs when it is a root, when VERIFY_KNOWN names its pid and start time, when a known
# process started it (a dead parent's pid stays in ParentProcessId, and a start time before the
# parent's rules out a reused pid), or when it is a dcp.exe told to --monitor a known pid. The last
# rule is the AppHost's: it starts DCP detached, so DCP, the dashboard, the API and Vite are not its
# children, and taskkill /T on the AppHost reaches none of them.
$live = @{}
Get-CimInstance Win32_Process | ForEach-Object { $live[[int]$_.ProcessId] = $_ }

# Every pid ever known, dead or alive, and when it started. A member is the live subset.
$start = @{}
$member = @{}
function Get-Start($p) { $p.CreationDate.ToUniversalTime().Ticks }

foreach ($root in ($env:VERIFY_ROOTS -split ' ' | Where-Object { $_ })) {
    $p = $live[[int]$root]
    if ($p) { $start[[int]$root] = Get-Start $p; $member[[int]$root] = $true }
}
if ($env:VERIFY_KNOWN -and (Test-Path $env:VERIFY_KNOWN)) {
    foreach ($line in Get-Content $env:VERIFY_KNOWN) {
        $field = $line -split "`t"
        if ($field.Count -lt 2) { continue }
        $start[[int]$field[0]] = [long]$field[1]
        $p = $live[[int]$field[0]]
        if ($p -and (Get-Start $p) -eq [long]$field[1]) { $member[[int]$field[0]] = $true }
    }
}

# A known pid vouches for its children only while it is dead or is still the process we recorded. Once
# Windows has given a dead member's pid to another program, that program and its children are not ours,
# even though they started after the member did.
function Test-Ours($pid_) { $start.ContainsKey($pid_) -and (-not $live[$pid_] -or $member[$pid_]) }

do {
    $grew = $false
    foreach ($p in $live.Values) {
        $id = [int]$p.ProcessId
        if ($member[$id]) { continue }
        $parent = [int]$p.ParentProcessId
        $byParent = (Test-Ours $parent) -and (Get-Start $p) -ge $start[$parent]
        $watches = $p.Name -eq 'dcp.exe' -and ([string]$p.CommandLine) -match '--monitor (\d+)' -and (Test-Ours ([int]$Matches[1]))
        if ($byParent -or $watches) {
            $member[$id] = $true
            $start[$id] = Get-Start $p
            $grew = $true
        }
    }
} while ($grew)

foreach ($id in ($member.Keys | Sort-Object)) { "{0}`t{1}`t{2}" -f $id, $start[$id], $live[$id].Name }
