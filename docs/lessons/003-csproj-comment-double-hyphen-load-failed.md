# A csproj comment that mentions a CLI flag stops the project loading

- **Status:** active
- **Date:** 2026-09-19
- **Plan version:** v01
- **Applies to:** `**/*.csproj`, `**/*.props`, `**/*.targets`, `*.slnx` · tags: msbuild, xml, visual-studio

## Symptom
Step 5 added a build-order `ProjectReference` to `NpcForge.Console/NpcForge.Console.csproj` with a why-comment above it. Visual Studio's Solution Explorer then showed `NpcForge.Console (load failed)` and, under it, only `The project file cannot be loaded.` `dotnet build NpcForge.Console` said why:
`NpcForge.Console.csproj(22,74): error MSB4025: The project file could not be loaded. An XML comment cannot contain '--', and '-' cannot be the last character. Line 22, position 74.`
Position 74 was the `--` of `--no-build` inside `<!-- ... dotnet run --no-build ... -->`.

## What did not work
- Solution Explorer. It gives no reason, no line, and no error in the Error List, so there was nothing to go on from inside VS.
- Reading the csproj by eye. Every tag was closed and the comment opened and closed, so it looked well-formed. `--` inside a comment does not look like XML syntax, so it does not look wrong.
- The earlier check. The `ProjectReference` had been built and verified on a scratch copy, but without its comment. The comment was written into the chat afterwards and handed over untested. "Verified" did not cover the text that was actually pasted.

## What worked
Run `dotnet build <project>` to get the MSBuild error with line and column. Reword the comment so it has no `--`: "starts the server with dotnet run and the no-build flag". Then Reload Project in VS, or let it reload the changed file.

## Why it works here, specifically
This project writes why-comments in its project files, and step 5's are about how the server is launched: `dotnet run --project ... --no-build`. CLI flags are exactly what those comments want to name, and every one of them starts with `--`. The rule is XML's, so it applies to every XML file here (`.csproj`, `.props`, `.slnx`) and not to C# or Markdown, where `--` in a comment is fine. A `-` just before the closing `-->` fails too.

## How to tell it is happening again
`MSB4025` or `An XML comment cannot contain '--'`; in VS, a project shown as `(load failed)` right after a comment was edited.
