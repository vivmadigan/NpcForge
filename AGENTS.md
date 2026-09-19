# AGENTS.md

NPC Dialogue Forge. A .NET console app for generating tabletop RPG characters
and their dialogue, built as a learning project.

## Read these, don't re-derive them

- `README.md` — what this is and why it is shaped this way. Read before any
  design discussion.
- `BUILD.md` — architecture, code sketches, the numbered build steps. Read
  before writing or reviewing any code.

Point me at the relevant section rather than restating it back to me.

## What this project is for

I am building this to learn how agent loops, Skills and MCP fit together.
Finished code handed to me is worth nothing here.

- Explain the approach and let me write it.
- When I ask for code, prefer a sketch of the shape over a full
  implementation.
- Review what I write and tell me plainly what is wrong with it.
- If I am about to do something that will not work, say so before I do it.
- If you see a small improvement that serves the goal, say it, even when I did
  not ask. One idea, a line on why, my call whether to take it. Not redesigns.
  Small advice at the right moment is what I value most.
- A sketch I can paste in, read through, then step through in the debugger is
  how I learn best. Where a step is about seeing something happen, make the
  block the whole file. Where it is about writing something, leave `...`.
- A sketch starts with its `using` lines. When a namespace comes from a package
  the project does not reference yet, say which package and which project it
  goes in. Pasting a sketch should leave only the `...` as errors.
- Once new code builds, suggest where to put breakpoints and what I should see
  at each one. Stepping through it in the debugger is part of every step.

## Easy to get wrong

- `roll_character` is a server tool that is **not** exposed to the model. The
  app calls it directly and puts the brief into the first message. See the
  diagram in BUILD.md.
- Two processes, one solution. No shared contracts project.
- A stdio MCP server must never write to stdout. That is the transport. Log to
  stderr or a file.
- Build steps run in order. Do not reach for MCP before the loop terminates on
  its own with a working cap.

## Vocabulary

- A **character brief** is the settled list of traits. Not a seed.
- **Rolling up a character** is producing that brief.

## Code

- Plain over clever. I want to be able to read it in six months.
- Modern C# where it names things. Not where it only compresses.
- No abstraction until there are two things to abstract.
- No Clean Architecture layering. Two interfaces earn their place:
  `IChatClient` (Microsoft.Extensions.AI, standing in for BUILD.md's
  `IChatModel`) and `IToolSource`.
- No host or DI container. `Program.cs` wires things by hand so the order of
  events stays readable: connect, roll, run.

## Build and run

```
dotnet build
dotnet test                                                                  # free: no network, no model calls
dotnet run --project NpcForge.Console                                        # OpenAI, default model
dotnet run --project NpcForge.Console -- --provider anthropic --model claude-opus-5
```

API keys live in user-secrets as `OpenAI:ApiKey` and `Anthropic:ApiKey`.

`NpcForge.Tests` (xUnit) holds three tests of `AgentLoop`. Two cover the rules
in BUILD.md "The loop": it returns when the model sends no tool calls, and it
throws when the turn cap is hit. The third runs a tool call through the real
MCP server. All use local fake chat clients, so they cost nothing to run. A
build fails while the app is running or paused in the debugger, because the
running app locks `NpcForge.Console.dll`.
