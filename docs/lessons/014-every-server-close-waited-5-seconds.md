# Every server a test started took 5 s to close, and the wait hid inside each test's time

- **Status:** history: the MCP server retired in epic 5 (ADR-016), and no test starts one
- **Date:** 2026-11-05
- **Applies to:** `NpcForge.Console/NpcForge.Console.csproj`, `NpcForge.Server/NpcForge.Server.csproj`, `NpcForge.Console/McpToolSource.cs`, any test that starts a server · tags: tests, speed, mcp

## Symptom

The free suite took 12 m 19 s. Each test that started a server took about 7.4 s, and two classes
that run their tests one after another, `McpToolSourceTests` and `LabRunTests`, made up the whole
wall time.

## What did not work

- The epic's guess, that `dotnet run` evaluating the project before every start was the main cost.
  It was 2.3 s of the 7.4 s.
- Setting `ShutdownTimeout` to 500 ms in `McpToolSource`. Dispose then took 0.55 s, but the client
  still killed a server that was never told to stop. That treats the symptom.
- Moving the server to 2.3.1 along with the console. The replay passed, but when a slow start makes
  the client's discovery probe time out, the client falls back to `initialize`, and a 2.3 server
  refuses it once that session has settled on the newer protocol version. With both on 2.3.1, 10 of
  455 tests failed on a loaded machine.

## What worked

Timing one server's phases in a scratch test: start 2.3 s, a tool call 0.1 s, dispose 5.1 s, and
the same 5.1 s with the server started from its dll. The server stayed alive and logged nothing for
the whole wait, and it exited 0.15 s after its stdin closed. So the ModelContextProtocol 2.2.0 client
waited out its 5 s default `ShutdownTimeout` with the server's stdin still open, then killed it.
SDK 2.3.0 closes stdin first (csharp-sdk pull request 1838). With only the console on 2.3.1, dispose
took 0.03 to 0.11 s and the suite took 206 s.

## Why it works here

Every run of the app starts one server and disposes it at exit (`Program.cs`), and the tests start
one or more per test, so a fixed wait at close was paid on every start. The fault was in the client
library, so the fix is there. The server needs no change, and its project file says why it stays on
2.2.0.

## How to tell it is happening again

A server's dispose takes almost exactly its `ShutdownTimeout`, and the server logs nothing after its
last request. On a healthy run, stderr ends with the server's own `[server]` lines for "shutting
down" and "shut down" after `[app] saved as`.
