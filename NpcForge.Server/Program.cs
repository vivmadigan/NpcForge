using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdout is the transport. Every log line goes to stderr instead.
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    // Finds every [McpServerToolType] class in this assembly by reflection; nothing here names
    // them. Forget the attribute and the build still passes: the server just starts with no
    // tools, and the model answers without them. The tools live in CharacterTools.cs.
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
