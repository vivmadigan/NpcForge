# Free test suite

`dotnet test` runs the repo's xUnit tests with fake models, the real dice and tools and the whole app against the recorded run. It uses no network and no model. It is the main regression net, and the way to show a new test fail with the code it guards removed.

## Sub-features

- `test-all` runs every test and passes.
- `test-fast` runs every test that starts no process, in seconds.
- `test-one` runs a single test or class by name.
- `test-breakit` shows a test fail with its guarded value replaced, then pass once restored.

## How to get to it (user POV)

- Run `dotnet test` in the repo root.
- Run `$V test`, or `$V test <name fragment>`, which runs `dotnet test --no-build` and records the output in `$D`.

## Driving it with npcforge-verify

Preconditions:

- `dotnet build` succeeded just now and `$V doctor` prints no `FAIL`. `--no-build` runs the last build.
- No `NpcForge.Console` process is running.

- **Run all.** Run `$V test`. It takes about 25 s (24 to 25 s over five runs once the server retired in epic 5), and longer beside other work. Exit code `0`, and `grep -E '^(Passed|Failed)!' "$D/test.txt"` prints `Passed!  - Failed:     0` with a non-zero `Passed` count.
- **Run the fast part.** Run `dotnet test --no-build --filter "Category!=Process"`. It runs the 449 tests that start no console, in a few seconds, and prints `Passed!` with `Total:   449`. Use it between edits, then run all before saying a task is done. A test that starts a process carries `[Trait("Category", "Process")]` (`ConsoleProcess.cs` says why); one without the trait slows this run down.
- **Run one class.** Run `$V test ReplayTests`. Exit code `0` and a `Passed!` line with `Total:     2`.
- **Filter that matches nothing.** Run `$V test NoSuchTestAnywhere`. The helper exits `65` and prints `no test ran`. A bare `dotnet test --filter` would exit `0` here.
- **Break it.** Follow the break-it loop in `../SKILL.md`. With the missing-field check replaced, `$V test A_required_field_left_out_is_refused_by_name` fails three times with `Assert.Throws() Failure: Exception type was not an exact match`, `Expected: typeof(NpcForge.RollRefusedException)` and `Actual: typeof(System.ArgumentException)`. After `git checkout -- NpcForge.Core/Brief/Dice.cs`, `git status --short` prints nothing, and after `dotnet build` the same test passes.
- **Proof.** `$D` holds `test.txt`, `test-ReplayTests.txt`, `test-NoSuchTestAnywhere.txt` and, for a break-it run, the failing and the passing `test-<name>.txt`, each with its `.exit`.

## Gotchas

- What each class guards, from `docs/architecture.md`. `AgentLoopTests` covers the exit on no tool calls, the turn cap and a tool call through the writer's real tool. `RollArgumentsTests` covers what the roll binds and what it refuses by name. `CharacterStoreTests` and `SaveFileGuardTests` cover a save loading back as saved and the owner's file kept byte for byte. `SeededRollTests` covers a handed-in value replacing its die without moving the others, and `DiceOrderTests` covers seeds 1 to 12 rolling their golden briefs. `ReplayTests` covers the whole app against the recording.
- A test that asserts on `Tool failed:` must pass arguments that would otherwise succeed, or it passes for the wrong reason (lesson 004). Check that when you add one.
- `ReplayTests` starts the console with `--no-build`, so it runs the last build of the console. Build first.
- Replace a value to break a test (`= null`, `= []`). Commenting out the body of an unbraced `if` gives CS1023 and no test runs.
- Chain build and test with `&&`. A failed build leaves the old binary, and `--no-build` then tests it.
- A new test is not proved by passing. Show it fail with the guarded code removed, as in the break-it loop, and report both outputs.
