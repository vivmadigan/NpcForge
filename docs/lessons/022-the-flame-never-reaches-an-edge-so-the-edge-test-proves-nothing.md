# The flame never reaches an edge, so a test of the edge rule passes with the rule removed

- **Status:** active
- **Date:** 2026-11-16
- **Applies to:** `NpcForge.Web/src/flameGrid.ts`, `NpcForge.Web/tests/flame.spec.ts` · tags: tests, web, flame, break-it

## Symptom

The test "steps the flame 1,000 frames and finds no lit cell on the top, left or right edge" passed
after the check that keeps the outer cells dark was deleted from `burn`. It was green when it should
have been red.

## What did not work

Stepping the real flame from its fuel bed. The bed is hot only in the middle, and every row cools it,
so the heat dies well before it meets a side or the top. The rule is never needed, and the test
cannot tell whether it is there.

## What worked

A second start for the same test: a grid lit to full heat everywhere inside its edges. Heat then
reaches the edges at once, so removing the left and right check fails at frame 1 with the lit cells
listed, and removing the top check fails the same way. The real flame's start stays in the test, and
a check that the grid stays lit keeps a dead grid from passing.

## Why it works here

A guard against a case that never happens needs the case made to happen. The worst case is the
input, not a long run of the normal one.

## How to tell it is happening again

A break-it check on a guard that stays green. Any rule about an edge or a limit that a test reaches
only through the program's own typical input.
