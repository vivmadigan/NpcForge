# A dev server left on port 5173 is the one the browser tests and checks run against

- **Status:** active
- **Date:** 2026-11-15
- **Applies to:** `NpcForge.Web/playwright.config.ts`, `NpcForge.Web/package.json`, any check that starts `npm run dev` or `npm start` · tags: tests, web, port, stale, windows

## Symptom

In epic 12's first PR, a check of the one start command printed `status=200 after 1s` for
`http://localhost:5173/saved/3`, while the command's own log showed it was still installing
(`up to date in 5s`). The answer came from a dev server an earlier step had started. `netstat`
showed a process listening on 5173 before the command under test had been run.

## What did not work

- Stopping the background task that ran `npm run dev`. The task ended and its Vite child kept
  listening.
- Reading a 200 from the port as proof that the command just run had started the app. Any server
  on the port answers.
- Running the check a second time. The first attempt's own server was by then the leftover, and the
  second attempt read its answer the same way.

## What worked

Look at the port before each run that matters and after the last one, and stop what is there by
process id:

```
netstat -ano | grep LISTENING | grep ":5173 "
taskkill //PID <pid> //T //F
```

Then read the started command's own log for Vite's ready line, not only the port's answer.

## Why it works here

`playwright.config.ts` sets `reuseExistingServer: true` (ADR-017, decision 2), so `npm test` uses
whatever is on port 5173 and starts nothing of its own. The scripts pass `--strictPort`, so a second
server cannot move to another port. Together they mean one server at a time, and the tests cannot
tell whose it is. A server left by another worktree would be tested in place of this one.

## How to tell it is happening again

A test result that does not change after an edit. A listening process on 5173 before anything was
started. A check that answers faster than the command's install step can finish.
