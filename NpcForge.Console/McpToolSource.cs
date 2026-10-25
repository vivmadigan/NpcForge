using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;      // McpClient, McpClientTool, StdioClientTransport
using ModelContextProtocol.Protocol;    // TextContentBlock
using System;
using System.Collections.Generic;
using System.Reflection;                // AssemblyConfigurationAttribute
using System.Text;
using System.Text.Json;

namespace NpcForge
{
    // The real tool source. Starts NpcForge.Server as a child process and talks MCP to it over
    // the server's stdin and stdout. The loop cannot tell this from FakeToolSource: same two
    // methods, same contract. That is what step 5 sets out to prove.
    public sealed class McpToolSource : IToolSource, IAsyncDisposable
    {
        // Owns the connection and, through its transport, the server process. Null until
        // ConnectAsync, which is why Program.cs connects before anything else runs.
        private McpClient? _client;

        // What the server said it has, asked for once at connect. Each McpClientTool keeps a
        // reference to the client, which is how tool.CallAsync below reaches the server.
        private IList<McpClientTool> _tools = [];

        // Tools only the app calls. A tool the model can see is a tool it can skip, so these
        // never reach its list. Save and load are the app's too: the model never touches the file.
        private static readonly string[] AppOnly = ["roll_character", "save_character", "load_character"];


        // The model's half of _tools. One place, so ListAsync and InvokeAsync can never
        // disagree about what the model may reach: a tool that is not on the list it was
        // given is not a tool it can call by naming it.
        private IEnumerable<McpClientTool> ModelTools => _tools.Where(t => !AppOnly.Contains(t.Name));

        // Which file the server saves characters in. Null leaves the server on its own default;
        // the tests pass a temp file so they never touch your saves.
        private readonly string? _charactersPath;

        // The seed for the server's dice. Null makes them random, whatever the shell holds.
        private readonly int? _seed;

        public McpToolSource(string? charactersPath = null, int? seed = null)
        {
            _charactersPath = charactersPath;
            _seed = seed;
        }

        // Start the server, shake hands, ask what tools it has.
        public async Task ConnectAsync(CancellationToken ct)
        {
            // Start from this program's bin folder and go up four levels to the repo root. Works for
            // both the app and the tests, whatever folder they were started from.
            var serverProject = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "NpcForge.Server"));

            // The server runs in the configuration this console was built in. `dotnet run --no-build`
            // with no -c falls back to Debug, so a Release console would drive a Debug server that
            // may be stale or missing, and a Release replay would prove nothing about the Release
            // server (lesson 008). The assembly carries the Configuration
            // the SDK built it under, so this is what `-c` set, not a guess from a path.
            var configuration = typeof(McpToolSource).Assembly
                .GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
                ?? throw new InvalidOperationException("The console assembly carries no build configuration.");

            // What the server is told, through its environment. The server inherits everything this
            // process has, and a null value here does not mean "leave it": it deletes the variable.
            // The two settings use that differently on purpose.
            //
            // NPCFORGE_SEED is always set. Only --seed seeds the dice, so a run without it must roll
            // random even when the shell still holds a NPCFORGE_SEED from an earlier session.
            //
            // NPCFORGE_CHARACTERS is set only when given. Program.cs passes the caller's own value, or
            // a temp file for a replay or a recording; a null here would delete a variable the
            // caller set, and the saves would land in the tracked characters.json.
            var environment = new Dictionary<string, string?>
            {
                ["NPCFORGE_SEED"] = _seed?.ToString(),
            };

            if (_charactersPath is not null)
                environment["NPCFORGE_CHARACTERS"] = _charactersPath;

            // Nothing starts yet: this only describes the process. The server's stdout is the
            // wire, so its logs go to stderr, and they arrive here with a [server] prefix.
            var transport = new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = "NpcForge.Server",
                Command = "dotnet",
                Arguments = ["run", "--project", serverProject, "--no-build", "-c", configuration],
                StandardErrorLines = line => Console.Error.WriteLine($"[server] {line}"),

                // Storage.cs and the dice read these. An environment variable reaches the child
                // process before any MCP message does, and survives the hop through dotnet run.
                // Inheriting is the library's default, written out because the server also needs
                // the rest of the caller's environment, such as the proxy the tests set.
                InheritEnvironmentVariables = true,
                EnvironmentVariables = environment,
            });

            // Library code from the ModelContextProtocol package. It starts the process, then runs
            // MCP's handshake (the server logs it as 'server/discover'). A factory method rather
            // than a constructor, because a constructor cannot await the server's reply.
            _client = await McpClient.CreateAsync(transport, cancellationToken: ct);

            // tools/list: names, descriptions and schemas, built on the server from the
            // attributes in CharacterTools.
            _tools = await _client.ListToolsAsync(cancellationToken: ct);
        }


        // What the model may ask for: everything the server has, minus the app-only tools. A new
        // list each time, holding the same tool objects, so the loop cannot change _tools.
        public Task<IReadOnlyList<AITool>> ListAsync(CancellationToken ct)
                                => Task.FromResult<IReadOnlyList<AITool>>([.. ModelTools]);


        // Same contract as FakeToolSource: one [tool] trace line, and every failure comes back as
        // text. An unknown name, a dead server and a closed pipe all land in the catch.
        public async Task<string> InvokeAsync(FunctionCallContent call, CancellationToken ct)
        {
            try
            {
                // On stderr so it never mixes with the answer. The server's own lines arrive on
                // the same channel with a [server] prefix, so the two read as one trace.
                Console.Error.WriteLine($"[tool] {call.Name} {JsonSerializer.Serialize(call.Arguments)}");

                // call.Name picks the tool, out of the same list ListAsync handed the model.
                // An app-only name and a name the server never had fail the same way here,
                // and the catch below turns both into text the model can read.
                var tool = ModelTools.FirstOrDefault(t => t.Name == call.Name)
                    ?? throw new InvalidOperationException($"No tool named '{call.Name}'.");


                // The call's arguments, parameter name to value. Here one pair: occupation ->
                // "innkeeper", as a JsonElement (step 2). Copied because CallAsync takes an
                // IReadOnlyDictionary and call.Arguments is an IDictionary.
                var args = call.Arguments?.ToDictionary(kv => kv.Key, kv => kv.Value);

                // tools/call, over the wire. The server runs LookupArchetype and sends back a list
                // of content blocks; a text tool sends one text block.
                var result = await tool.CallAsync(args, cancellationToken: ct);

                var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

                // A tool that throws on the server does not throw here: the SDK sends back
                // IsError with a generic message. Same prefix as the catch below, so the model
                // sees one shape for a failure, wherever it happened.
                return result.IsError is true ? $"Tool failed: {text}" : text;

            }
            catch (Exception ex)
            {
                return $"Tool failed: {ex.Message}";          // a result, not an exception
            }
        }

        // The app's own door to the server, for the app-only tools. The model is not part of this
        // call, so a failure throws instead of coming back as text.
        public async Task<string> CallDirectAsync(string name, Dictionary<string, object?> args, CancellationToken ct)
        {
            var result = await _client!.CallToolAsync(name, args, cancellationToken: ct);
            var text = string.Join("\n", result.Content.OfType<TextContentBlock>().Select(c => c.Text));

            // No model has been asked anything yet. A failed roll handed on as "the settled brief"
            // would have the model invent the character, so stop the run here.
            if (result.IsError is true)
                throw new InvalidOperationException($"{name} failed: {text}");

            return text;
        }


        // Closes the connection, and with it the server process, on purpose. Without this the
        // server stops only because the app exited and the pipe closed under it.
        public async ValueTask DisposeAsync()
        {
            if (_client is not null)
                await _client.DisposeAsync();
        }

    }
}
