using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace NpcForge.Api;

// The model the API calls: which provider and model, and how to make the client behind them. A record
// registered in DI, so a test can put its own client under the forge without a setting (ADR-019,
// decision 10). `Create` is called at the first model call and never at start-up, so the saved page
// works with no key, as the console's --list does (decision 3).
public sealed record ModelChoice(string Provider, string Model, Func<IChatClient> Create);

// The forge: every interview and every kept draft, in memory until the process stops (ADR-019,
// decision 3). Nothing expires. The routes are thin and every rule of the interview, the roll and the
// save is the core's, called as the console calls it.
public sealed class Forge(ModelChoice model, int? seed, ICharacterStore store)
{
    // Made once and kept. PublicationOnly does not cache a factory that throws, so a key set after a
    // refusal is found on the next try without a restart.
    private readonly Lazy<IChatClient> _client = new(model.Create, LazyThreadSafetyMode.PublicationOnly);

    private readonly ConcurrentDictionary<string, Interview> _interviews = new();
    private readonly ConcurrentDictionary<string, KeptDraft> _drafts = new();

    // One interview. Its dice and its cap live and die with it, so a roll again takes the next dice and
    // draws on the same allowance, as in the console (ADR-013, decision 3). The gate lets one request at
    // a time work on it, because Dice and SpendingCap are not safe across threads.
    private sealed class Interview(SceneDraft draft, Dice dice, SpendingCap cap)
    {
        public SceneDraft Draft { get; } = draft;
        public Dice Dice { get; } = dice;
        public SpendingCap Cap { get; } = cap;
        public SemaphoreSlim Gate { get; } = new(1, 1);
    }

    // A written draft and the task of its first save. A second save of the draft finds the task and
    // waits for it, so a double click or a retry converges on one save (principle-make-operations-idempotent).
    private sealed class KeptDraft(Draft draft)
    {
        public Draft Draft { get; } = draft;
        public Task<int>? Saving { get; set; }
    }

    // The scene in one go: the model's turns over it, then the first step.
    public async Task<IResult> StartAsync(string? scene, CancellationToken ct)
    {
        // Trimmed as the console trims what the GM types, and a blank one is no scene.
        scene = scene?.Trim();
        if (string.IsNullOrEmpty(scene))
            return Problems.Of(StatusCodes.Status400BadRequest, "No scene", "Describe the scene first.");

        // The 503 is mapped around the client's creation and nothing else: its error type is one a bug
        // elsewhere throws too (ADR-019, decision 4). SpendingCapReachedException is one as well.
        IChatClient client;
        try
        {
            client = _client.Value;
        }
        catch (InvalidOperationException fault)
        {
            return Problems.Of(StatusCodes.Status503ServiceUnavailable, "No model to call", fault.Message);
        }

        var cap = new SpendingCap(client, SpendingCap.TokensPerSession);
        try
        {
            var draft = await SceneInterview.ReadAsync(cap, ModelOptions.New(model.Model), Dice.Fields, scene, ct);
            var id = NewId();
            _interviews[id] = new Interview(draft, new Dice(seed), cap);
            return StepOf(id, draft);
        }
        catch (SpendingCapReachedException stop)
        {
            return Stopped(stop);
        }
    }

    // The GM's reply to the open question. Answering calls no model.
    public async Task<IResult> AnswerAsync(string id, string? text, CancellationToken ct)
    {
        if (!_interviews.TryGetValue(id, out var interview))
            return NoInterview(id);

        await interview.Gate.WaitAsync(ct);
        try
        {
            var recorded = interview.Draft.Answer(text ?? "");
            return recorded.Accepted
                ? StepOf(id, interview.Draft)
                : Problems.Of(StatusCodes.Status422UnprocessableEntity, "Answer refused", recorded.Message);
        }
        finally
        {
            interview.Gate.Release();
        }
    }

    // The GM's yes: roll, name and write, as the console's loop does. A second call on one interview is
    // a roll again, and takes the interview's next dice.
    public async Task<IResult> RollAsync(string id, CancellationToken ct)
    {
        if (!_interviews.TryGetValue(id, out var interview))
            return NoInterview(id);

        await interview.Gate.WaitAsync(ct);
        try
        {
            // The console never rolls before every question is answered, and RollArguments could not be
            // built with a hole in it.
            if (interview.Draft.NextQuestion is { } open)
                return Problems.Of(StatusCodes.Status409Conflict, "A question is still open",
                    $"Answer every question before rolling. {open.Name} has no answer.");

            var ask = new Ask(interview.Draft.RollArguments(), interview.Draft.Scene, model.Provider);
            var draft = await CharacterRun.RollAndWriteAsync(interview.Cap, ModelOptions.New(model.Model), interview.Dice, ask, ct);

            var draftId = NewId();
            _drafts[draftId] = new KeptDraft(draft);
            return Results.Ok(new WrittenRun(draftId, draft.Written.Name.Full, draft.Written.Text));
        }
        catch (NoNameException ex)
        {
            // No name followed the dice, so the writer was never called: nothing is written to keep.
            return Results.Ok(new NoNameRun(ex.Message));
        }
        catch (RollRefusedException ex)
        {
            return Problems.Of(StatusCodes.Status422UnprocessableEntity, "The dice refused the roll", ex.Reason);
        }
        catch (SpendingCapReachedException stop)
        {
            return Stopped(stop);
        }
        finally
        {
            interview.Gate.Release();
        }
    }

    // Keeps a draft in the saves. Only the first call saves: the rest get its number.
    public async Task<IResult> SaveAsync(string id)
    {
        if (!_drafts.TryGetValue(id, out var kept))
            return Problems.Of(StatusCodes.Status404NotFound, "No such draft", "No draft with that id is held. A restart of the API forgets every draft.");

        try
        {
            Task<int> saving;
            lock (kept)
            {
                // Not cancelled with the request: the task is shared, and the first caller leaving must
                // not cancel the save a second caller is waiting on.
                saving = kept.Saving ??= CharacterRun.SaveAsync(store, kept.Draft, CancellationToken.None);
            }

            try
            {
                return Results.Ok(new SavedAs(await saving));
            }
            catch
            {
                // A save that faulted is not the draft's save, so a retry, after the owner mends the file,
                // can try again.
                lock (kept)
                {
                    if (ReferenceEquals(kept.Saving, saving))
                        kept.Saving = null;
                }
                throw;
            }
        }
        catch (InvalidDataException fault)
        {
            return Problems.Unreadable(fault);
        }
    }

    // The next question, or the confirmation once every question is answered.
    private static IResult StepOf(string id, SceneDraft draft) =>
        draft.NextQuestion is { } field
            ? Results.Ok(new QuestionStep(id, field.Name, field.Description))
            : Results.Ok(new ConfirmStep(id, [.. draft.Settled().Select(line => new ConfirmEntry(line.Field.Name, line.Value))]));

    private static IResult Stopped(SpendingCapReachedException stop) => Results.Ok(new StoppedStep(stop.Message));

    private static IResult NoInterview(string id) =>
        Problems.Of(StatusCodes.Status404NotFound, "No such interview", $"No interview with id {id} is held. A restart of the API forgets every interview.");

    private static string NewId() => Guid.NewGuid().ToString("N");
}
