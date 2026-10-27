# A PR merged before its last push leaves master without its re-record

- **Status:** active
- **Date:** 2026-10-27
- **Applies to:** `NpcForge.Tests/Fixtures/replay.jsonl`, `NpcForge.Server/characters.json`, any PR that needs a paid re-record · tags: workflow, replay, PR

## Symptom

Three times in epic 3, `master` held a behaviour change without the replay fixture that matches it,
so the replay tests failed there:
- #25 (unit 6) was merged a minute after it was opened. The re-record and the owner's save 7 were
  pushed to its branch after that, so they never reached `master` and needed #26.
- #27 (unit 7) and #28 (unit 8) were merged before their re-records, which came later as #30.

## What did not work

Saying in chat that a PR was "ready" while a commit for it was still to come. The owner reviews and
merges on GitHub, often straight away, and a push to a branch after its PR is merged goes nowhere.
Opening the PR before the paid re-record, with "needs a re-record" in its body, also did not stop a
merge: the PR looked green apart from the replay.

## What worked

Treating the re-record as part of the PR and nothing as ready until it is in: the agent says a PR is
ready only once nothing more will be pushed to it. When a merge comes first anyway, a follow-up PR
from `master` carries the missing commits (#26, #30). Its tree is checked to be the tested one before
it is opened.

## Why it works here

Paid re-records happen on the owner's machine, after the agent's work is done, so there is always a
gap between "the code is done" and "the PR is complete". Naming the re-record as the last step of the
PR, not a step after it, closes that gap.

## How to tell it is happening again

`dotnet test` on `master` fails only in `ReplayTests` and `OfflineTests` with "Model call 1 differs",
straight after a behaviour PR merged. Check whether the PR's branch got a commit after its merge
time.
