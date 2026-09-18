# OpenAI rejects function tools on Chat Completions when reasoning is on

- **Status:** active
- **Date:** 2026-09-13 (recorded 2026-09-18 from PLAN.md "Decisions already made")
- **Plan version:** v01
- **Applies to:** NpcForge.Console/ModelClients.cs · tags: openai, sdk, tools

## Symptom
HTTP 400 from OpenAI on `gpt-5.6-terra` when `ChatOptions.Tools` contains a function tool and the request goes to Chat Completions. The message says to use `/v1/responses` or set `reasoning_effort` to `none`.

## What did not work
- Forcing `reasoning_effort` to `none`. It makes the call succeed but puts a vendor-specific workaround on `ChatOptions`, which is shared with the Anthropic client, and switches off reasoning for the model that has it.

## What worked
Building the OpenAI client on the Responses endpoint (`OpenAI.Responses.ResponsesClient`) instead of Chat Completions. `ModelClients.cs` has `#pragma warning disable OPENAI001` because the SDK marks Responses experimental.

## Why it works here, specifically
The loop depends on `IChatClient` only. Swapping the endpoint changed the concrete type from `OpenAIChatClient` to `OpenAIResponsesChatClient` with no edit to `ChatAgent.cs` or `AgentLoop.cs`. That is the abstraction earning its place, and it is why the fix belongs in `ModelClients.cs` and nowhere else.

## How to tell it is happening again
`400` with the text "use /v1/responses" in the response body, on any OpenAI reasoning model with tools in the request.
