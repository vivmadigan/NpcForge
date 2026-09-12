using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NpcForge;

string provider = "openai";
string model = "gpt-5.4-mini";

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--provider" && i + 1 < args.Length)
        provider = args[i + 1].ToLower();
    if (args[i] == "--model" && i + 1 < args.Length)
        model = args[i + 1].ToLower();
}

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

Startup.ConfigureServices(builder, provider, model);
var host = builder.Build();

await ChatAgent.RunAsync(host.Services);
