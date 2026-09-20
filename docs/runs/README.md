# Runs

One file per paid run: what was asked, what came back, and what it showed. The point is not
the collection, it is the comparison — the same four answers run against a changed skill or a
changed prompt, so a tweak can be judged instead of guessed at.

## How a run gets here

1. **Commit first.** Runs cost money; a commit is free. The SHA in the header is only worth
   recording if the tree was clean when the run happened, otherwise it names the wrong code.
2. Run the app and watch it live.
3. Select all in the console, paste it into a new file here.
4. Name it `YYYY-MM-DD-NN-<short-slug>.md`.

Do not redirect the console to a file in PowerShell. It buffers stdout and stderr separately
and merges them late, so the answer and the `[app]`/`[loop]` traces come out in the wrong
order — and the ordering is the information. `cmd /c "... > file 2>&1"` preserves it, but then
nothing shows on screen while the run costs money. Pasting is simpler and it works.

## The header

Model, the four answers, the rolled obstacle, which skill file and its fingerprint, the commit,
and the token counts. Most of it can be read straight off the `[app]`, `[skill]` and `[loop]`
trace lines in the paste.

## Where observations live

Under the step in `PLAN.md`, as they already do. There is deliberately no index here: one
observation should have one home, and `PLAN.md` is imported into every session. If this folder
ever grows past the point where that works, revisit it then.

## Why not, for the three obvious alternatives

Each of these was the first design, and each was dropped for a reason that is not visible from
the outcome. Written down so they are not quietly reinstated.

**Why not redirect the run to a file instead of pasting?** Because PowerShell reorders the two
streams, and the ordering is the information. Full account, including what the throwaway
experiment showed: [lesson 005](../lessons/005-powershell-redirect-reorders-trace.md).

**Why "commit first" rather than letting `git diff` between two runs' SHAs say what changed?**
That was the original plan, and the challenger killed it. Tuning a skill happens against a dirty
tree — that is the normal state, not an edge case — so two runs made an hour apart carry the same
SHA and `git diff <sha1> <sha2>` is empty. Worse at the time: `SKILL.md` was untracked, so a SHA
pair said nothing about the only file being changed. The fix is that a run file has to stand on
its own. Commit before a paid run so the SHA means something, and let the `[skill]` line carry a
content fingerprint so the run names its own guidance whatever git says. A char count alone is
not enough for that: two same-length edits give the same count and different hashes.

**Why no `INDEX.md` here?** Because the observations already have a home in `PLAN.md`'s step
notes, which is imported into every session, and an index would be a second place for the same
sentence to live. Two homes for one observation drift apart; one that nobody reads is worse than
none. The folder listing is the index until it stops being enough.
