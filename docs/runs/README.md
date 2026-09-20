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
