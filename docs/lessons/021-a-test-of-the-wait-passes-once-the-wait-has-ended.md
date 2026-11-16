# A test of the wait passes once the wait has ended, and a held page clock hangs the contrast check

- **Status:** active
- **Date:** 2026-11-16
- **Applies to:** `NpcForge.Web/tests/wait.spec.ts`, `NpcForge.Web/src/api.ts`, any test that must look at the wait screen · tags: tests, web, wait, axe, timers

## Symptom

The test that the black fades in over the old step, and that the old step is then hidden, passed with
the code that hides the old step removed. Its last checks were `toBeHidden` on the old step's text box
and the contrast check. The made-up seam answers after one second, and the test ran past it. The old
step was "hidden" because the wait had ended and the next step had replaced it, and the contrast check
judged the next step and not the wait.

## What did not work

- **Holding the seam with the page clock.** `page.clock.install()` and `pauseAt` before the click
  kept the wait open. But axe uses timers, so `contrastFindings` hung until the test timed out, and
  the cavern's `requestAnimationFrame` never ran, so `createImageData` threw when the clock moved on.
- **Checking sooner.** A faster test still races a one-second wait on a slow machine.

## What worked

An init script that keeps the page's one-second timer (the seam's) and runs it only when the test
calls `answerTheSeam`. Every other timer is the real one, so axe runs. The wait then stays up for as
long as the test wants, the checks run inside it, and the answer is released by hand. The same hold
catches a wait the page adds after the answer: after the release the next step must show within
500 ms. It cannot see a shorter one, or a least time on screen that runs beside the seam.
Show the break-it for each check: remove the code that hides the old step, and add a 1,500 ms timer
to `send`.

## Why it works here

The seam is a timer in the made-up code, and the page's only job is to show the wait until it
fires. A test that controls that timer sees the wait and nothing else.

## How to tell it is happening again

A wait test that passes with its code broken, or whose element checks pass because the element is
gone. A contrast test on the wait that times out. A change of the seam's delay from 1,000 ms, which
the init script matches.
