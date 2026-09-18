# PreToolUse hook: decision records and plan versions are written once, never edited.
#
# Claude Code sends the tool call as JSON on stdin. If the target file already
# exists under docs/decisions/ or docs/plans/ and is not an INDEX.md, exit 2.
# Exit 2 blocks the tool call and sends the stderr text back to the agent as
# the reason. Any other exit code lets the call through.
#
# Two things stay allowed, because they are how the history is meant to grow:
#   1. Creating a NEW file in those folders. Superseding is a new record, not an edit.
#   2. An Edit whose new text contains "Superseded by". Marking an old record as
#      superseded is the one legal change to it.

$ErrorActionPreference = 'Stop'

try {
    # Windows PowerShell 5.1 reads stdin in the OEM code page by default; Claude Code sends UTF-8.
    try { [Console]::InputEncoding = New-Object System.Text.UTF8Encoding($false) } catch { }

    $raw = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

    $hook = $raw | ConvertFrom-Json
    $path = $hook.tool_input.file_path
    if ([string]::IsNullOrWhiteSpace($path)) { exit 0 }

    # Normalise so the same check works for C:\repo\docs\plans\x.md and /repo/docs/plans/x.md.
    $normalised = ($path -replace '\\', '/')

    $protected = $normalised -match '/docs/(decisions|plans)/[^/]+$'
    if (-not $protected) { exit 0 }

    $name = [System.IO.Path]::GetFileName($normalised)
    if ($name -ieq 'INDEX.md') { exit 0 }

    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { exit 0 }   # new file: allowed

    $isEdit = $hook.tool_name -eq 'Edit'
    $newText = [string]$hook.tool_input.new_string
    if ($isEdit -and $newText -imatch 'Superseded by') { exit 0 }        # the one legal edit (case-insensitive)

    [Console]::Error.WriteLine("Blocked: $name is a decision record or plan version and is immutable once written. Write a new file that supersedes it (next number) and add it to INDEX.md. The only edit allowed to the old file is its status line, changed to 'Superseded by <new record>'.")
    exit 2
}
catch {
    # A broken hook must never block real work. Log and let the call through.
    [Console]::Error.WriteLine("protect-docs.ps1: could not evaluate hook input ($($_.Exception.Message)); allowing the call.")
    exit 0
}
