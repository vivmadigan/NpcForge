# A test that starts many servers at once fails one run in three

- **Status:** active
- **Date:** 2026-10-30
- **Applies to:** `NpcForge.Tests/DiceOrderTests.cs`, any test that starts several `McpToolSource` servers · tags: tests, flaky, server

## Symptom

`Seeds_1_to_12_roll_the_golden_briefs_at_every_difficulty` rolls 36 briefs, each on a server of its
own. It passed, then failed on the very next run with no code change, after 1 m 6 s. A third run
passed.

## What did not work

Starting all 36 servers together with one `Task.WhenAll`. Each server is a `dotnet run --project
... --no-build`, which evaluates the project before it starts. With 36 of them at once on a 16-core
machine, some took longer than the MCP client's start-up wait, and the connect failed. Running the
36 one after another would have been stable, and would have taken minutes.

## What worked

A `SemaphoreSlim(6)` around each roll (`DiceOrderTests.RollSixAtATime`). The test then took about
42 s, and passed three runs of three.

## Why it works here

The failure was load, not logic. Each roll needs its own server, because the dice are a static
seeded once per server process, so the number of servers cannot shrink. Capping how many start at
once keeps each start inside the wait.

## How to tell it is happening again

A test that starts servers in parallel fails at about one minute, on connect, and passes on a rerun
with no change. Epic 9 (faster tests) may make each start cheaper. Until then, cap the fan-out.
