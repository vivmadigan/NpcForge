using Microsoft.Extensions.AI;
using NpcForge;
using NpcForge.Api;

var builder = WebApplication.CreateBuilder(args);

// One store, on the file the caller named. It is the API's one port (ADR-019, decision 5).
builder.Services.AddSingleton<ICharacterStore>(new FileCharacterStore(SavesPath.From(builder.Configuration).Value));

// The model the forge calls, from the console's own defaults so the site calls the model the console
// calls (ADR-019, decision 6). `Seed` fixes the dice of every interview, as --seed does the console's.
var provider = builder.Configuration["Provider"] ?? ModelClients.DefaultProvider;
var model = builder.Configuration["Model"] ?? ModelClients.DefaultModel;
var seed = builder.Configuration.GetValue<int?>("Seed");

// The stand-in is picked here, before ModelClients.Create, which knows no such provider, so the
// console's providers do not change (ADR-019, decision 7). Any other provider is the console's, built
// at the forge's first model call. The offline stop is read from configuration, which holds the
// environment too, so a test can set it with a setting and not on its own process.
builder.Services.AddSingleton(new ModelChoice(provider, model, provider == "stand-in"
    ? () => new StandInChatClient()
    : () => ModelClients.Create(provider, model, builder.Configuration,
        offline: builder.Configuration[ModelClients.OfflineVariable] == "1")));

builder.Services.AddSingleton(services => new Forge(
    services.GetRequiredService<ModelChoice>(), seed, services.GetRequiredService<ICharacterStore>()));

// Every tone the dice can roll needs a file in the skill folder, and so does every field a scene can
// leave out. A gap stops the API here, before an interview is paid for and not when a roll lands on it
// (ADR-009, ADR-010, ADR-019 decision 3), as the console stops before its interview.
ToneFiles.Require(Dice.Fields, [WriterInput.DefaultSkillDir]);
NotGivenFiles.Require(Dice.Fields, [WriterInput.DefaultSkillDir]);

var app = builder.Build();

// The name arrives joined, as the console's list joins it. NamedCharacter.Full stays the one place that
// does it (ADR-019, decision 3).
static string? Joined(CharacterName? name) => name is null ? null : new NamedCharacter(name.Syllables, name.Family).Full;

app.MapGet("/api/saves", async (ICharacterStore store, CancellationToken ct) =>
{
    try
    {
        var listed = await store.ListAsync(ct);
        return Results.Ok(listed.Select(row => new SaveRow(row.Number, Joined(row.Name), row.Occupation, row.Setting)));
    }
    catch (InvalidDataException fault)
    {
        return Problems.Unreadable(fault);
    }
});

// {number:int}: a path that is no int, or no int that fits, matches no route and is a plain 404, which
// the page reads as "no such save", as it did before there was an API.
app.MapGet("/api/saves/{number:int}", async (int number, ICharacterStore store, CancellationToken ct) =>
{
    try
    {
        return await store.LoadAsync(number, ct) is { } saved
            ? Results.Ok(new SaveSheet(number, Joined(saved.Name), saved.Text))
            : Problems.Of(StatusCodes.Status404NotFound, "No such save", $"No save numbered {number}.");
    }
    catch (InvalidDataException fault)
    {
        return Problems.Unreadable(fault);
    }
});

// The forge's four routes. Each only hands its arguments to Forge, which holds every rule.
app.MapPost("/api/interviews", (SceneBody body, Forge forge, CancellationToken ct) => forge.StartAsync(body.Scene, ct));

app.MapPost("/api/interviews/{id}/answers", (string id, AnswerBody body, Forge forge, CancellationToken ct) => forge.AnswerAsync(id, body.Text, ct));

app.MapPost("/api/interviews/{id}/characters", (string id, Forge forge, CancellationToken ct) => forge.RollAsync(id, ct));

app.MapPut("/api/drafts/{id}/save", (string id, Forge forge) => forge.SaveAsync(id));

app.Run();
