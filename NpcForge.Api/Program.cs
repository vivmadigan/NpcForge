using NpcForge;
using NpcForge.Api;

var builder = WebApplication.CreateBuilder(args);

// One store, on the file the caller named. It is the API's one port so far (ADR-019, decision 5).
builder.Services.AddSingleton<ICharacterStore>(new FileCharacterStore(SavesPath.From(builder.Configuration).Value));

var app = builder.Build();

// The name arrives joined, as the console's list joins it. NamedCharacter.Full stays the one place
// that does it (ADR-019, decision 3).
static string? Joined(CharacterName? name) => name is null ? null : new NamedCharacter(name.Syllables, name.Family).Full;

// A save file that will not parse is the store's InvalidDataException, and it is the only fault caught
// here: anything else is a bug, and the host answers it as one (ADR-019, decision 4).
static IResult Unreadable(InvalidDataException fault) =>
    Results.Problem(title: "The saves file could not be read", detail: fault.Message, statusCode: StatusCodes.Status500InternalServerError);

app.MapGet("/api/saves", async (ICharacterStore store, CancellationToken ct) =>
{
    try
    {
        var listed = await store.ListAsync(ct);
        return Results.Ok(listed.Select(row => new SaveRow(row.Number, Joined(row.Name), row.Occupation, row.Setting)));
    }
    catch (InvalidDataException fault)
    {
        return Unreadable(fault);
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
            : Results.Problem(title: "No such save", detail: $"No save numbered {number}.", statusCode: StatusCodes.Status404NotFound);
    }
    catch (InvalidDataException fault)
    {
        return Unreadable(fault);
    }
});

app.Run();
