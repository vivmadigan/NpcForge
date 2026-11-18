// The one command: `dotnet run --project NpcForge.AppHost` starts the API and the website's pages
// together (ADR-019, decision 9).
var builder = DistributedApplication.CreateBuilder(args);

// The saves the API reads. The caller's own NPCFORGE_CHARACTERS wins, which is how an agent points a run
// at a scratch file. Without one it is the owner's NpcForge.Console/characters.json, found from this
// project's folder and made absolute (ADR-019, decision 5).
var own = builder.Configuration["NPCFORGE_CHARACTERS"];

// `dotnet run` starts this process in its project folder and DCP starts the API in its own, so neither
// knows the folder a relative path was meant from. The API refuses one too (SavesPath), but under the
// AppHost that refusal shows only in the dashboard, so the command stops here, in the caller's terminal.
if (!string.IsNullOrWhiteSpace(own) && !Path.IsPathFullyQualified(own))
    throw new InvalidOperationException(
        $"NPCFORGE_CHARACTERS is \"{own}\", a relative path. The AppHost and the API run in their own project folders, "
        + "not yours, so it would name a different file. Set it to an absolute path.");

var saves = string.IsNullOrWhiteSpace(own)
    ? Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "NpcForge.Console", "characters.json"))
    : own;

var api = builder.AddProject<Projects.NpcForge_Api>("api")
    .WithEnvironment("NPCFORGE_CHARACTERS", saves);

// Aspire adds its own --port to the dev script, and Vite honours the last one, so the scripts keep
// their fixed 5173 for `npm start` and need no change (ADR-019, "Context"). WithReference gives Vite
// the API's address as API_HTTP, which vite.config.ts proxies /api to.
builder.AddViteApp("web", "../NpcForge.Web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
