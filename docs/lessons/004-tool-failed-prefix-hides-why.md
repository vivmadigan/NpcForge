# A test asserting on "Tool failed:" can pass for the wrong reason

- **Status:** active
- **Date:** 2026-09-20
- **Plan version:** v01 (see docs/plans/INDEX.md)
- **Applies to:** `NpcForge.Tests/**`, `NpcForge.Console/McpToolSource.cs` · tags: testing, mcp, tool-source

## Symptom

`Refuses_an_app_only_tool_the_model_names` was written to prove step 7's carry-over: that
`McpToolSource.InvokeAsync` refuses an app-only name like `roll_character` instead of
finding it in the unfiltered `_tools`. It passed. It also passed with the fix reverted —
with `var tool = _tools.First(t => t.Name == call.Name);` restored, the full suite still
reported `Passed! - Failed: 0, Passed: 5`.

A test that passes whether or not the code under test is there proves nothing.

## What did not work

The first version built the call with no arguments:

```csharp
var call = new FunctionCallContent("call-1", "roll_character");
```

Without the filter, that name *is* found in `_tools`, so `tool.CallAsync(null, ...)` ran and
the server rejected it for missing the four required parameters (`setting`, `playersWant`,
`difficulty`, `occupation`). `InvokeAsync` turned that into `"Tool failed: ..."` — the same
prefix, and the text still contained `roll_character`, so **both** assertions held:

```csharp
Assert.StartsWith("Tool failed:", result);
Assert.Contains("roll_character", result);
```

Strengthening the assertions would not have helped. Any message-matching made the test more
brittle without separating the two causes, because both failures are real failures of the
same call.

## What worked

Give the call arguments that would genuinely succeed. Then the only remaining reason for a
failure is the filter:

```csharp
var call = new FunctionCallContent("call-1", "roll_character", new Dictionary<string, object?>
{
    ["setting"] = "an inn in a major city",
    ["playersWant"] = "the name of a fence",
    ["difficulty"] = "Wall",
    ["occupation"] = "innkeeper",
});
```

Verified both ways on a scratch copy: with the fix `Passed: 5`; with `_tools.First` restored,
`Refuses_an_app_only_tool_the_model_names [FAIL]`.

## Why it works here, specifically

`IToolSource.InvokeAsync`'s contract, set in step 4 and kept by `McpToolSource`, is that
*every* failure comes back as text with one prefix so the model sees one shape — an unknown
name, a dead server, a closed pipe and a server-side `IsError` all become `"Tool failed: "`.
That is deliberate and good for the model, and it is exactly what makes the prefix useless as
a test assertion on its own. The prefix says a call failed; it never says why. Any test in
this project that asserts on it must first rule out every other way that call could fail.

This bites again at step 8, when `save_character` and `load_character` join `AppOnly` and get
the same test.

## How to tell it is happening again

A test asserting `StartsWith("Tool failed:", ...)` still passes after you revert the code it
was written to cover. Always run a new `IToolSource` test once with the fix removed.
