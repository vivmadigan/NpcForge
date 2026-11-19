using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;                // ConfigureTestServices
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NpcForge.Api;

namespace NpcForge.Tests;

// ADR-019, decisions 3, 4 and 10: the forge's four routes, run in-process on a temp saves file and the
// stand-in model, so no test starts a process or a port, calls a model or sits outside the fast part.
// Every expected value is read off the roll's own field list, the temp file or the core called directly,
// never off the API's code.
public class ForgeApiTests : IDisposable
{
    private readonly List<string> _files = [];

    public void Dispose()
    {
        foreach (var file in _files)
            File.Delete(file);
    }

    private string NewSavesPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"npcforge-forge-{Guid.NewGuid():N}.json");
        _files.Add(path);
        return path;
    }

    // One choice word, "Wall", which the stand-in finds and records as the difficulty, in a sentence of its
    // own. The scene says nothing of the fields the interview must ask for, so each is asked. No digit and
    // no other choice of any field is in it, so nothing else is recorded.
    private const string Scene = "The players meet a baker in a market town and ask about a stolen cart. She is a Wall.";

    private const string Model = "stand-in-model";
    private const int Seed = 7;

    private static readonly List<RollField> Asked = [.. Dice.Fields.Where(field => field.WhenMissing == WhenMissing.Ask)];

    // What the GM types for a question, from the field's name.
    private static string TypedFor(string field) => $"typed for {field}";

    private static async Task<JsonElement> Body(HttpResponseMessage response)
    {
        using var stream = await response.Content.ReadAsStreamAsync();
        return (await JsonDocument.ParseAsync(stream)).RootElement.Clone();
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Body(response);
    }

    private static string? Text(JsonElement element, string property) =>
        element.GetProperty(property).ValueKind == JsonValueKind.Null ? null : element.GetProperty(property).GetString();

    private static async Task<JsonElement> Start(HttpClient client, string scene = Scene) =>
        await Ok(await client.PostAsJsonAsync("/api/interviews", new { scene }));

    // Answers every question the interview asks, in the order it asks them, and returns the confirmation.
    private static async Task<JsonElement> AnswerEverything(HttpClient client, JsonElement step)
    {
        while (Text(step, "kind") == "question")
        {
            var answer = await client.PostAsJsonAsync(
                $"/api/interviews/{Text(step, "interview")}/answers", new { text = TypedFor(Text(step, "field")!) });
            step = await Ok(answer);
        }
        return step;
    }

    private static async Task<JsonElement> Roll(HttpClient client, string interview) =>
        await Ok(await client.PostAsync($"/api/interviews/{interview}/characters", null));

    // Done when: a scene, a question, its answer, the confirmation, a roll, a save, the list and a load.
    [Fact]
    public async Task A_scene_is_walked_to_a_saved_character_that_the_list_and_a_load_read_back()
    {
        var file = NewSavesPath();
        using var api = new ForgeHost(file);
        var client = api.CreateClient();

        // The questions, one at a time, each as the roll's own field says it.
        var step = await Start(client);
        var interview = Text(step, "interview")!;
        var questions = new List<(string Field, string Question)>();
        while (Text(step, "kind") == "question")
        {
            Assert.Equal(interview, Text(step, "interview"));
            var field = Text(step, "field")!;
            questions.Add((field, Text(step, "question")!));
            step = await Ok(await client.PostAsJsonAsync($"/api/interviews/{interview}/answers", new { text = TypedFor(field) }));
        }
        Assert.Equal(Asked.Select(field => (field.Name, field.Description)), questions);

        // The confirmation holds what was settled in the roll's order: the answers typed, the choice the
        // scene named, and a null for the want the scene never gave. A field the dice will pick has none.
        var settled = new Dictionary<string, string> { ["difficulty"] = "Wall" };
        foreach (var field in Asked)
            settled[field.Name] = TypedFor(field.Name);
        Assert.Equal("confirm", Text(step, "kind"));
        Assert.Equal(interview, Text(step, "interview"));
        var entries = step.GetProperty("entries").EnumerateArray().Select(entry => (Text(entry, "field")!, Text(entry, "value")));
        Assert.Equal(
            Dice.Fields
                .Where(field => settled.ContainsKey(field.Name) || field.WhenMissing == WhenMissing.NotGiven)
                .Select(field => (field.Name, settled.GetValueOrDefault(field.Name))),
            entries);

        // The roll, and the character it wrote.
        var written = await Roll(client, interview);
        Assert.Equal("written", Text(written, "kind"));
        var name = Text(written, "name")!;
        Assert.False(string.IsNullOrWhiteSpace(name));
        Assert.Equal(StandInChatClient.Sheet, Text(written, "sheet"));

        // The save, and the file it joined.
        var saved = await Ok(await client.PutAsync($"/api/drafts/{Text(written, "draft")}/save", null));
        Assert.Equal(1, saved.GetProperty("number").GetInt32());
        using var onDisk = JsonDocument.Parse(File.ReadAllText(file));
        var entry = onDisk.RootElement.GetProperty("1");
        Assert.Equal(StandInChatClient.Sheet, entry.GetProperty("text").GetString());
        Assert.Equal("Wall", entry.GetProperty("brief").GetProperty("difficulty").GetString());
        Assert.Equal(TypedFor("setting"), entry.GetProperty("brief").GetProperty("setting").GetString());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("brief").GetProperty("playersWant").ValueKind);

        // The list and a load read that save back.
        var rows = (await Ok(await client.GetAsync("/api/saves"))).EnumerateArray().ToList();
        var row = Assert.Single(rows);
        Assert.Equal(1, row.GetProperty("number").GetInt32());
        Assert.Equal(name, Text(row, "name"));
        Assert.Equal(TypedFor("occupation"), Text(row, "occupation"));
        Assert.Equal(TypedFor("setting"), Text(row, "setting"));
        var opened = await Ok(await client.GetAsync("/api/saves/1"));
        Assert.Equal(name, Text(opened, "name"));
        Assert.Equal(StandInChatClient.Sheet, Text(opened, "sheet"));
    }

    // ADR-019, decision 10: one seed and one scene, through the core as Program.cs calls it and through the
    // API, and every request each side sends a model is the same, in order. A difference here is the
    // website sending a model something the console would not.
    [Fact]
    public async Task The_API_sends_a_model_the_requests_the_console_sends_for_the_same_seed_and_scene()
    {
        // Spaces round the scene, because the console trims what the GM types and the API trims what the
        // page sends, and a request that went untrimmed would differ.
        var typed = "  " + Scene + "  ";

        // Through the core, as Program.cs wires it: the recorder where RecordingChatClient sits, the cap
        // outside it, one options object for every roll and a copy for the interview, one Dice.
        var coreLog = new RequestLog(new StandInChatClient());
        IChatClient client = new SpendingCap(coreLog, SpendingCap.TokensPerSession);
        var options = ModelOptions.New(Model);
        var dice = new Dice(Seed);
        var gm = new StringReader(typed + "\n" + string.Concat(Asked.Select(field => TypedFor(field.Name) + "\n")) + "yes\n");
        var told = await SceneInterview.RunAsync(client, options.Clone(), Dice.Fields, gm, TextWriter.Null, CancellationToken.None);
        var ask = new Ask(told!.RollArguments, told.Scene, "stand-in");
        await CharacterRun.RollAndWriteAsync(client, options, dice, ask, CancellationToken.None);
        await CharacterRun.RollAndWriteAsync(client, options, dice, ask, CancellationToken.None);

        // Through the API, with the same seed: the start, the answers and two rolls.
        var apiLog = new RequestLog(new StandInChatClient());
        using var api = new ForgeHost(NewSavesPath(), builder =>
        {
            builder.UseSetting("Seed", Seed.ToString());
            builder.ConfigureTestServices(services => services.AddSingleton(new ModelChoice("stand-in", Model, () => apiLog)));
        });
        var http = api.CreateClient();
        var confirm = await AnswerEverything(http, await Start(http, typed));
        await Roll(http, Text(confirm, "interview")!);
        await Roll(http, Text(confirm, "interview")!);

        // The control: both rolls reached the writer, so the comparison below covers the whole run.
        Assert.Equal(2, coreLog.Requests.Count(request => request.Contains("name: npc-writer")));
        Assert.Equal(coreLog.Requests.Count, apiLog.Requests.Count);
        for (var at = 0; at < coreLog.Requests.Count; at++)
            Assert.True(coreLog.Requests[at] == apiLog.Requests[at],
                $"Model call {at + 1} differs.\nconsole: {coreLog.Requests[at]}\nAPI: {apiLog.Requests[at]}");
    }

    // ADR-019, decision 3: a draft is saved once, by construction. Two saves at once, as a double click
    // sends them, both answer with the one number and the file holds one save.
    [Fact]
    public async Task Two_saves_of_one_draft_at_once_return_one_number_and_save_once()
    {
        var file = NewSavesPath();
        using var api = new ForgeHost(file);
        var client = api.CreateClient();
        var confirm = await AnswerEverything(client, await Start(client));
        var draft = Text(await Roll(client, Text(confirm, "interview")!), "draft");

        var both = await Task.WhenAll(client.PutAsync($"/api/drafts/{draft}/save", null), client.PutAsync($"/api/drafts/{draft}/save", null));

        var numbers = new List<int>();
        foreach (var response in both)
            numbers.Add((await Ok(response)).GetProperty("number").GetInt32());
        Assert.Equal([1, 1], numbers);
        using var onDisk = JsonDocument.Parse(File.ReadAllText(file));
        Assert.Single(onDisk.RootElement.EnumerateObject());
    }

    // The task of a save that faulted is not the draft's save: the owner mends the file and saves again.
    [Fact]
    public async Task A_save_into_a_file_that_will_not_parse_is_a_500_and_a_retry_after_mending_it_saves()
    {
        var file = NewSavesPath();
        File.WriteAllText(file, "{ not json");
        using var api = new ForgeHost(file);
        var client = api.CreateClient();
        var confirm = await AnswerEverything(client, await Start(client));
        var draft = Text(await Roll(client, Text(confirm, "interview")!), "draft");

        var refused = await client.PutAsync($"/api/drafts/{draft}/save", null);

        Assert.Equal(HttpStatusCode.InternalServerError, refused.StatusCode);
        Assert.StartsWith($"Could not read {file}: ", (await Body(refused)).GetProperty("detail").GetString());

        File.WriteAllText(file, "{}");
        var retried = await Ok(await client.PutAsync($"/api/drafts/{draft}/save", null));
        Assert.Equal(1, retried.GetProperty("number").GetInt32());
    }

    // ADR-019, decision 3: each interview has its own spending cap. The stand-in reports a whole cap's
    // tokens in one call, so the interviewer's first call spends it and the next is refused, before the
    // model is asked.
    [Fact]
    public async Task An_interview_that_reaches_the_cap_stops_and_no_model_call_follows_the_one_that_spent_it()
    {
        var log = new RequestLog(new StandInChatClient { TokensPerCall = SpendingCap.TokensPerSession });
        using var api = new ForgeHost(NewSavesPath(), builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(new ModelChoice("stand-in", Model, () => log))));

        var step = await Start(api.CreateClient());

        Assert.Equal("stopped", Text(step, "kind"));
        Assert.Equal(new SpendingCapReachedException(SpendingCap.TokensPerSession, SpendingCap.TokensPerSession).Message, Text(step, "reason"));
        Assert.Single(log.Requests);
    }

    // The same cap on the roll: five calls spend it exactly (the interview's two, the namer's two and the
    // writer's one), so the character is written and the roll again is refused at its first call.
    [Fact]
    public async Task A_roll_again_that_reaches_the_cap_stops_and_the_first_character_stands()
    {
        var log = new RequestLog(new StandInChatClient { TokensPerCall = SpendingCap.TokensPerSession / 5 });
        using var api = new ForgeHost(NewSavesPath(), builder =>
            builder.ConfigureTestServices(services => services.AddSingleton(new ModelChoice("stand-in", Model, () => log))));
        var client = api.CreateClient();
        var confirm = await AnswerEverything(client, await Start(client));
        var interview = Text(confirm, "interview")!;

        var first = await Roll(client, interview);
        var again = await Roll(client, interview);

        Assert.Equal("written", Text(first, "kind"));
        Assert.Equal("stopped", Text(again, "kind"));
        Assert.Equal(5, log.Requests.Count);
    }

    [Theory]
    [InlineData("POST", "/api/interviews/nope/answers")]
    [InlineData("POST", "/api/interviews/nope/characters")]
    [InlineData("PUT", "/api/drafts/nope/save")]
    public async Task An_interview_or_draft_the_API_does_not_hold_is_a_404_problem(string method, string path)
    {
        using var api = new ForgeHost(NewSavesPath());
        var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "POST" && path.EndsWith("answers") ? JsonContent.Create(new { text = "x" }) : null,
        };

        var response = await api.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("""{ "scene": "" }""")]
    [InlineData("""{ "scene": "   " }""")]
    [InlineData("{}")]
    public async Task A_blank_scene_is_a_400_problem(string body)
    {
        using var api = new ForgeHost(NewSavesPath());

        var response = await api.CreateClient().PostAsync("/api/interviews", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    // The console never rolls before every question is answered, and RollArguments cannot be built with a
    // hole in it, so the API refuses and the interview stays as it was.
    [Fact]
    public async Task A_roll_with_a_question_still_open_is_a_409_problem_and_the_question_stays_open()
    {
        using var api = new ForgeHost(NewSavesPath());
        var client = api.CreateClient();
        var step = await Start(client);
        Assert.Equal("question", Text(step, "kind"));

        var response = await client.PostAsync($"/api/interviews/{Text(step, "interview")}/characters", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(Text(step, "field")!, (await Body(response)).GetProperty("detail").GetString());
    }

    // A blank answer is the draft's own refusal, and the same question is still the next.
    [Fact]
    public async Task A_blank_answer_is_a_422_problem_and_the_same_question_is_still_open()
    {
        using var api = new ForgeHost(NewSavesPath());
        var client = api.CreateClient();
        var step = await Start(client);
        var interview = Text(step, "interview");

        var response = await client.PostAsJsonAsync($"/api/interviews/{interview}/answers", new { text = "   " });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var next = await Ok(await client.PostAsJsonAsync($"/api/interviews/{interview}/answers", new { text = "an inn" }));
        Assert.Equal(Asked[1].Name, Text(next, "field"));
    }

    // ADR-019, decision 4: the offline stop answers 503 with ModelClients' own message. Production loads
    // no user secrets, so if the stop were not passed through the answer would be the missing key's
    // message and this would fail for free: nothing paid is reached either way.
    [Fact]
    public async Task A_provider_the_offline_stop_refuses_is_a_503_problem_with_the_stops_message()
    {
        using var api = new ForgeHost(NewSavesPath(), builder =>
        {
            builder.UseSetting("Provider", "openai");
            builder.UseSetting(ModelClients.OfflineVariable, "1");
        });
        var expected = Assert.Throws<InvalidOperationException>(
            () => ModelClients.Create("openai", Model, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), offline: true)).Message;

        var response = await api.CreateClient().PostAsJsonAsync("/api/interviews", new { scene = Scene });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expected, (await Body(response)).GetProperty("detail").GetString());
    }

    // ADR-019, decision 3: the key is read at the first model call, so the saved page works with a model
    // that cannot be built.
    [Fact]
    public async Task The_saved_page_still_works_when_the_model_cannot_be_built()
    {
        using var api = new ForgeHost(NewSavesPath(), builder =>
        {
            builder.UseSetting("Provider", "openai");
            builder.UseSetting(ModelClients.OfflineVariable, "1");
        });

        var response = await api.CreateClient().GetAsync("/api/saves");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
