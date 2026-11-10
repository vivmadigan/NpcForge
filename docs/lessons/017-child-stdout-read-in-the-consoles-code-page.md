# A test that reads a child's output in the console's encoding reads the same bytes two ways

- **Status:** active
- **Date:** 2026-11-10
- **Applies to:** `NpcForge.Tests/ConsoleProcess.cs`, any test that starts a process and reads its output · tags: tests, flaky, encoding, process

## Symptom

`RollAgainTests.A_seeded_run_that_rolls_again_gives_the_same_two_characters_every_time` runs the same
seeded replay twice and compares the two stdouts. In epic 5's behavior PR, once the suite stopped
starting MCP servers, it failed in 3 of 4 full `dotnet test` runs:

```
Expected: ···"within earshot.  \n> ÔÇ£Come in, then. I h"···
Actual:   ···"within earshot.  \n> “Come in, then. I hop"···
```

The same bytes, read as UTF-8 in one run and in the console's code page in the other. PR 58 had seen
it fail 1 of 10 times and called it a known flake.

## What did not work

- Taking the first run of a session to be the odd one out: the console app sets the shared console's
  output code page to UTF-8, so the first child would be read in the old code page and every later one
  in UTF-8. Run alone, the test is the session's first and passed 2 of 2 times, so that is not the
  whole story.
- Treating it as a flake to rerun. A test that compares two runs of the app cannot pass by chance
  while the decoding is chosen by state outside the test.

## What worked

Naming the encoding. `ConsoleProcess` sets `StandardOutputEncoding` and `StandardErrorEncoding` to
UTF-8 without a byte order mark, which is what the app writes on both streams (`Program.cs` sets
`Console.OutputEncoding` first, lesson 006). The full suite then passed 5 runs of 5.

## Why it works here

The app always writes UTF-8, whatever the console it runs under. With no encoding named, .NET reads a
redirected stream in an encoding taken from the console's state, and that state is shared with every
process on the console and changed by the tests running beside this one. The exact cause of each
change was not pinned down; naming the encoding removes the dependence on it.

## How to tell it is happening again

A curly quote or a dash in a child's output reads as `ÔÇ£`, `ÔÇÖ` or `ÔÇö` in one run and right in
another, or two runs of the same seeded replay differ only there. Any `ProcessStartInfo` with
`RedirectStandardOutput` and no `StandardOutputEncoding`.
