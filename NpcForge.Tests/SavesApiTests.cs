using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;                // ConfigureTestServices
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;                 // Environments
using NpcForge.Api;

namespace NpcForge.Tests;

// ADR-019, decisions 3 to 5 and 10: the API's two save routes, run in-process on a temp saves file, so no
// test starts a process or a port and all of them sit in the fast part. Every expected value is read off
// the file the test wrote, never off the API's code. No model is built or called: the API has no model
// code yet.
public class SavesApiTests : IDisposable
{
    private readonly List<string> _files = [];

    public void Dispose()
    {
        foreach (var file in _files)
            File.Delete(file);
    }

    // Keyed on a type inside the API and not on Program, because the console has a Program too.
    private sealed class Api(string path, Action<IServiceCollection>? services = null) : WebApplicationFactory<SavesPath>
    {
        // The path goes in as the setting the AppHost sets as an environment variable. A blank one is
        // how the refusal is reached from here, because a missing one would read the shell's own
        // NPCFORGE_CHARACTERS, which an agent sets.
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Production, so a fault the API does not catch reaches the test as the exception. In
            // Development the host's developer page would turn it into a 500 and hide whether it was caught.
            builder.UseEnvironment(Environments.Production);
            builder.UseSetting(SavesPath.Variable, path);
            if (services is not null)
                builder.ConfigureTestServices(services);
        }
    }

    private string Write(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"npcforge-api-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, contents);
        _files.Add(path);
        return path;
    }

    // The numbers are not in order as text (10 and 11 sort before 2 and 9). Save 2 has no name and no
    // setting, 9 has an occupation that is a JSON null and no name, 10 has no occupation, and 11 has all four.
    private static readonly string FourSaves = JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["11"] = new
        {
            brief = new { setting = "a river barge", occupation = "ferryman", tone = "Heartfelt" },
            name = new { syllables = new[] { "Tam" }, family = "Reed" },
            text = SavedFile.TextMarker,
        },
        ["2"] = new { brief = new { occupation = "goblin baker" }, text = SavedFile.TextMarker },
        ["10"] = new
        {
            brief = new { setting = "an inn in a major city", playersWant = "the name of a fence" },
            name = new { syllables = new[] { "Har", "grim" }, family = "Rockhammer" },
            text = SavedFile.TextMarker,
        },
        ["9"] = new { brief = new { setting = "a tiny bakery", occupation = (string?)null }, text = SavedFile.TextMarker },
    });

    // CRLF, curly quotes, a dash and a line that ends in two spaces: what a normalising layer would change.
    private const string Sheet = "## The Baker\r\n\r\nShe says “don’t” — twice.  \nAnd leaves.\n";

    private static readonly string TwoSavesWithSheets = JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["4"] = new
        {
            brief = new { setting = "a tiny bakery", occupation = "baker" },
            name = new { syllables = new[] { "Aldo", "Bun" }, family = "Sourdough" },
            text = Sheet,
        },
        ["5"] = new { brief = new { setting = "a market" }, text = "An old save, with no name." },
    });

    private static JsonElement Parse(string body) => JsonDocument.Parse(body).RootElement;

    private static string? Text(JsonElement element, string property) =>
        element.GetProperty(property).ValueKind == JsonValueKind.Null ? null : element.GetProperty(property).GetString();

    [Fact]
    public async Task The_list_is_every_save_in_number_order_with_the_name_joined_and_a_gap_left_as_null()
    {
        var file = Write(FourSaves);
        using var api = new Api(file);

        var response = await api.CreateClient().GetAsync("/api/saves");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var rows = Parse(body).EnumerateArray().ToList();
        Assert.Equal([2, 9, 10, 11], rows.Select(row => row.GetProperty("number").GetInt32()));
        Assert.Equal(
            [
                (null, "goblin baker", null),
                (null, null, "a tiny bakery"),
                ("Hargrim Rockhammer", null, "an inn in a major city"),
                ("Tam Reed", "ferryman", "a river barge"),
            ],
            rows.Select(row => (Text(row, "name"), Text(row, "occupation"), Text(row, "setting"))));

        // The page's type has these four fields, and a gap is a null in the field, not a missing key.
        Assert.All(rows, row => Assert.Equivalent(new[] { "number", "name", "occupation", "setting" }, row.EnumerateObject().Select(field => field.Name).ToArray(), strict: true));
        // A list reads the name and the brief, never the text, and writes nothing.
        Assert.DoesNotContain(SavedFile.TextMarker, body);
        Assert.Equal(FourSaves, File.ReadAllText(file));
    }

    [Fact]
    public async Task The_list_of_a_missing_file_is_empty()
    {
        var file = Path.Combine(Path.GetTempPath(), $"npcforge-api-{Guid.NewGuid():N}.json");
        using var api = new Api(file);

        var response = await api.CreateClient().GetAsync("/api/saves");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, Parse(await response.Content.ReadAsStringAsync()).GetArrayLength());
        Assert.False(File.Exists(file), "listing writes nothing");
    }

    [Fact]
    public async Task A_save_opens_with_its_number_its_joined_name_and_its_text_exactly_as_saved()
    {
        using var api = new Api(Write(TwoSavesWithSheets));

        var response = await api.CreateClient().GetAsync("/api/saves/4");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var save = Parse(await response.Content.ReadAsStringAsync());
        Assert.Equivalent(new[] { "number", "name", "sheet" }, save.EnumerateObject().Select(field => field.Name).ToArray(), strict: true);
        Assert.Equal(4, save.GetProperty("number").GetInt32());
        Assert.Equal("AldoBun Sourdough", Text(save, "name"));
        Assert.Equal(Sheet, Text(save, "sheet"));
    }

    [Fact]
    public async Task An_old_save_with_no_name_opens_with_a_null_name()
    {
        using var api = new Api(Write(TwoSavesWithSheets));

        var save = Parse(await api.CreateClient().GetStringAsync("/api/saves/5"));

        Assert.Null(Text(save, "name"));
        Assert.Equal("An old save, with no name.", Text(save, "sheet"));
    }

    [Theory]
    [InlineData("/api/saves/6")]
    [InlineData("/api/saves/0")]
    [InlineData("/api/saves/-4")]
    public async Task A_number_no_save_holds_is_a_404_problem_that_names_it(string path)
    {
        using var api = new Api(Write(TwoSavesWithSheets));

        var response = await api.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.Equal($"No save numbered {path["/api/saves/".Length..]}.", problem.GetProperty("detail").GetString());
    }

    // The page sends whatever digits are in its address, and a number past an int is no save, as it was
    // before there was an API. Without the route's int constraint this is a 400, and the page has no
    // state for one.
    [Theory]
    [InlineData("/api/saves/99999999999")]
    [InlineData("/api/saves/abc")]
    public async Task A_number_that_is_no_int_is_a_404_and_not_a_bad_request(string path)
    {
        using var api = new Api(Write(TwoSavesWithSheets));

        var response = await api.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // The store's message is the one the console reports for the same file (ADR-015, decision 4).
    [Theory]
    [InlineData("{ not json", "")]
    [InlineData("""{ "1": { "text": "no brief" } }""", "entry 1 has no brief or no text.")]
    public async Task A_save_file_that_will_not_parse_is_a_500_problem_with_the_stores_message(string contents, string ending)
    {
        var file = Write(contents);
        using var api = new Api(file);
        var client = api.CreateClient();

        foreach (var path in new[] { "/api/saves", "/api/saves/1" })
        {
            var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var detail = Parse(await response.Content.ReadAsStringAsync()).GetProperty("detail").GetString()!;
            Assert.StartsWith($"Could not read {file}: ", detail);
            Assert.EndsWith(ending, detail);
        }
        Assert.Equal(contents, File.ReadAllText(file));
    }

    // Only the unreadable file is caught. A fault the API cannot name is a bug, and it must reach the
    // host and not be told to the page as a bad file (principle-boundary-discipline).
    [Theory]
    [InlineData("/api/saves")]
    [InlineData("/api/saves/1")]
    public async Task Any_other_fault_in_the_store_is_not_reported_as_an_unreadable_file(string path)
    {
        using var api = new Api(Write("{}"), services => services.AddSingleton<ICharacterStore>(new BrokenStore()));

        var fault = await Assert.ThrowsAsync<InvalidOperationException>(() => api.CreateClient().GetAsync(path));

        Assert.Equal("the store is broken", fault.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void With_no_saves_path_the_API_refuses_to_start_and_names_the_variable(string blank)
    {
        using var api = new Api(blank);

        var refusal = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("NPCFORGE_CHARACTERS", refusal.ToString());
    }

    // The rule is FileCharacterStore's, held by FileCharacterStoreTests; this is the one start-up that shows
    // the API reaches it. The positive control is every other test here, which passes a full temp path and starts.
    [Fact]
    public void With_a_relative_saves_path_the_API_refuses_to_start_and_asks_for_a_full_one()
    {
        using var api = new Api("saves.json");

        var refusal = Assert.ThrowsAny<Exception>(() => api.CreateClient());

        Assert.Contains("\"saves.json\" is not a full path with its drive", refusal.ToString());
        Assert.Contains("NPCFORGE_CHARACTERS", refusal.ToString());
    }

    private sealed class BrokenStore : ICharacterStore
    {
        public Task<int> SaveAsync(SavedCharacter character, CancellationToken ct) => throw new InvalidOperationException("the store is broken");

        public Task<SavedCharacter?> LoadAsync(int number, CancellationToken ct) => throw new InvalidOperationException("the store is broken");

        public Task<IReadOnlyList<ListedCharacter>> ListAsync(CancellationToken ct) => throw new InvalidOperationException("the store is broken");
    }
}
