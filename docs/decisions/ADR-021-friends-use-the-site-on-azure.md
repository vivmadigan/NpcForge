# ADR-021: Friends use the site on Azure, through one run that owns its cap and reports its steps

- **Status:** accepted
- **Date:** 2026-11-27
- **Supersedes:** no record whole. It amends ADR-014 (decisions 3, 4 and 7, and three
  consequences), ADR-016 (decision 4), ADR-017 (decisions 2 and 3) and ADR-019 (decisions 2 to 10,
  one line of its context and three consequences). It changes AGENTS.md lines in the PRs listed in
  Consequences.

## Context

Epic 6, "Friends can use it". A few friends the owner invites sign in to the website, describe a
scene, and get and keep their own characters, with spending capped per person and in total. The
epic left six choices to the owner and asks for a design check and this record before anything is
built. An architecture review on 2026-11-27 found five ways to deepen the code. The owner took the
first, a `CharacterRun` that owns one interview's run, and the fifth, one seam for what a run
reports. Both land in this epic. The second, `AgentLoop` setting its tools on a copy of the
options, merged first as PR #86.

The design check ran on 2026-11-27 in one session. For each choice, the options and their costs
were set out with a recommendation, and the owner decided. The owner wants as much practice in
Azure and GitHub as the work allows, and is sometimes short on time.

The code showed these facts:

- `Program.cs` and `Forge.cs` each build a `SpendingCap`, the options and a `Dice`, and each catches
  `NoNameException`, `RollRefusedException` and `SpendingCapReachedException`. `ForgeApiTests` keeps a
  hand-made copy of `Program.cs`'s wiring to prove the two agree.
- Since PR #86, the caller's `ChatOptions` come back from `AgentLoop` as they went in, so one set
  serves every loop of a run.
- The core writes 12 trace lines to `Console.Error` from 6 files: `AgentLoop`, `ConsoleTool`,
  `ChatAgent`, `Namer`, `CharacterRun` and `SceneInterview`. In the API's log two interviews'
  lines interleave with nothing to tell them apart.
- `Forge` holds every interview and draft in memory. A second copy of the API would not see them.
- 21 test call sites build `AgentLoop`, `ConsoleTool` or call `ChatAgent.WriteAsync`,
  `SceneInterview.RunAsync`, `ReadAsync` or `Namer.RunAsync` directly.
- `NpcForge.Console/characters.json` holds the owner's 16 saves.
- The 2026-11-08 bakery run, one interview and three characters, used 18,298 input and 10,338
  output tokens (`docs/runs/2026-11-08-01-epic4-checks.md`).
- The owner's machine has Docker 29.1.3 and LocalDB. It has no Azure CLI, no `azd`, and Aspire's CLI
  at 13.2.2. The repository is public on GitHub. It has no CI.

The docs showed these facts, read on 2026-11-27:

- Aspire 13's `AddAzurePostgresFlexibleServer(...).RunAsContainer()` runs a Postgres container
  locally. Published, it provisions Azure Database for PostgreSQL Flexible Server: Burstable
  `Standard_B1ms`, 32 GB, version 16, with Entra sign-in on and passwords off. Plain `AddPostgres`
  publishes Postgres as a container app.
- `aspire deploy` to Azure Container Apps provisions a Basic container registry (about $0.167 a
  day), a managed identity, a Log Analytics workspace and the environment.
  `PublishWithContainerFiles` copies a Vite app's built files into another resource's container.
- An Azure free account covers 750 hours of B1ms and 32 GB of Postgres storage a month for 12
  months. Container Apps gives 180,000 vCPU-seconds, 360,000 GiB-seconds and 2 million requests
  free each month.
- SQLite on an Azure Files share fails to lock (reports of `SQLITE_BUSY` and corruption), so SQLite
  cannot live on Container Apps' storage.
- Microsoft Entra External ID is free for the first 50,000 monthly users. Sign-up for strangers can
  be turned off only through Microsoft Graph, and a forum answer reports side effects.
- gpt-6-luna is listed at $0.10 per million input tokens and $0.50 per million output tokens, from
  third-party pages that quote OpenAI's. At that price the bakery run cost about $0.007.
- An OpenAI project can enforce a monthly hard spend limit, after which calls fail with a 429.
- .NET 10 has `TypedResults.ServerSentEvents`.
- Aspire runs `aspire deploy` from GitHub Actions with `azure/login` and a federated credential, and
  can cache its deployment state between runs.

## Options considered

Where it is hosted, and which database:
- **Fly.io, one machine, SQLite on its disk.** About $3 to $6 a month, the fewest parts, and the
  store's tests on the real engine with no Docker. Not Azure, and Aspire would not deploy it.
- **Azure Container Apps with Azure SQL's free offer.** About $5 a month. The free database pauses
  when idle, so a first click after a quiet spell waits for it to resume.
- **A small server running Docker Compose from `aspire publish`.** About €4 to €5 a month, and the
  owner keeps the server patched and backed up.
- **Azure Container Apps with Neon's free Postgres.** About $5 a month. A second company, a database
  password to keep, and a database Aspire does not deploy.
- **Azure Container Apps with Azure's Flexible Server, both from the AppHost.** Free for a year on a
  new account apart from the registry, then about $20 to $25 a month (an estimate). No database
  password exists. Chosen by the owner, for the Azure practice.

How friends sign in:
- **Container Apps' built-in sign-in.** Almost no code, but it exists only when deployed, so the
  local site and the deployed site behave differently.
- **ASP.NET Core Identity, with accounts in Postgres.** Works offline and in free tests. The site
  stores password hashes, and with no email sender the owner resets a forgotten password by hand.
- **Microsoft Entra External ID, with the API doing the sign-in.** Chosen by the owner.

The caps:
- **In tokens, as today.** No price to keep right, but a pricier model would let more money
  through unseen.
- **In characters a month.** Easy for a friend to read, and only loosely tied to money.
- **In dollars, from a price setting beside the model setting.** Chosen. The goal is stated in money.

The owner's console saves:
- **Kept apart.** The site never shows the 16.
- **The console moves to the database.** It would need the network and an Azure sign-in to list,
  load or save, and a second store for replays and tests.
- **Copied once, then kept apart.** Chosen by the owner.

Who deploys:
- **The owner runs `aspire deploy`.** No new parts.
- **An agent runs it after the owner's yes.** The epic's default.
- **GitHub Actions on each merge.** The most practice, and no time per deploy once set up. Chosen,
  after one deploy by hand.

The run's shape:
- **Static methods that return outcomes.** Both front ends would still build the cap, the dice
  and the options.
- **A run object that checks a core port for the monthly caps.** Postgres is outside the process,
  so the port needs a second implementation and puts money rules in the core.
- **A run object, with the monthly caps as a wrapper around the model client in the API.** Chosen.

What a run reports, and how the page hears it:
- **Steps only, through `IProgress<T>`.** The smallest change. The API's log stays mixed.
- **`ILogger` in the core.** Widens the core's references, needs a formatter to keep the console's
  bytes, and steps would still need their own channel.
- **One report seam for the trace lines and the steps.** Chosen.
- **The page polls for the step.** Plain JSON, but a roll runs on after its tab closes.
- **The roll's answer is a stream of events.** Chosen.

## Decision

1. **The site runs on Azure Container Apps, deployed by `aspire deploy`.** The AppHost gains a
   Container Apps environment. The API runs as one copy at most, because `Forge` holds interviews in
   memory, and by default one copy stays running ("Left to the owner"). The pages are built into the API's container with `PublishWithContainerFiles(web,
   "./wwwroot")`, and the API serves them, answering `/`, `/saved` and `/saved/{number}` with
   `index.html`. So the page and the API share one address and the sign-in cookie needs no
   cross-site rule. On the owner's machine `npm start` and `AddViteApp` stay as they are, and
   `vite.config.ts` also forwards the sign-in paths to the API. The new Aspire packages are pinned
   at 13.6.5 (ADR-020).

2. **The database is Postgres, from one line of the AppHost.**
   `AddAzurePostgresFlexibleServer("postgres").RunAsContainer()` gives a Postgres container on the
   owner's machine and Azure's Flexible Server when deployed, at Aspire's default size. On the
   owner's machine the container keeps its data in a Docker volume (`WithDataVolume`), so saves
   outlive a restart. In Azure the API signs in to it with its managed identity, so no database
   password exists there.

   | Table | Holds |
   | --- | --- |
   | `saves` | person, number, the brief as text, the name's syllables and family, the text. One row per save, keyed by person and number |
   | `people` | person, the email seen at the latest sign-in. The copy in decision 5 finds the owner's shelf by it |
   | `spend` | person, month in UTC, dollars |
   | `keys` | ASP.NET Core's data protection keys |

   The brief is a `text` column, never `jsonb`, which reorders keys, so an old save is written back
   byte for byte (ADR-009, ADR-014 decision 2). The Postgres store lives in `NpcForge.Api`, its only
   user, and the API builds one per person for each request, as ADR-014's Consequences planned. It
   passes `CharacterStoreTests`. A save takes one past that person's highest number inside one
   transaction that first locks that person's numbers (`pg_advisory_xact_lock`), so two saves at
   the same moment get different numbers (ADR-015). Postgres's default isolation alone would let
   both read the same highest number. The data
   protection keys live in the database, so a restart does not sign everyone out. Whether the store
   uses EF Core or Npgsql alone is the build's choice.

3. **Friends sign in through Microsoft Entra External ID, and the API does the sign-in.** The owner
   creates the external tenant and its app registration. The API signs a person in with OpenID
   Connect and gives the page a cookie, so the browser holds no token. A person is their External ID
   object id.

   - **The invite list is a setting that holds email addresses, compared ignoring case.** The API checks it on
     every request, so removing an address locks that person out at once. A sign-in from an address
     not on it gets 403 "not invited", and nothing is stored for it. Turning off sign-up for
     strangers in the tenant is a second lock the owner may add, never the rule.
   - **An interview and a draft belong to the person who started them.** A request from anyone else
     gets the 404 an unknown id gets.
   - **The pages gain a sign-in, a sign-out and a not-invited state.** A 401 from any call shows the
     sign-in.
   - **Free tests and free runs sign in through a stand-in scheme**, picked by a setting, as the
     stand-in model is. Two locks keep it off the deployed site. The API refuses to start the
     stand-in sign-in with any model but the stand-in, and the deployed site always runs a paid
     model. The API also refuses either stand-in when Container Apps runs it, which it tells by an
     environment variable Container Apps sets. The first deploy confirms which variable, and the
     first lock does not depend on it. A test holds each refusal.

4. **Three caps, each checked before a model call.**

   | Cap | Where | Limit |
   | --- | --- | --- |
   | Per run | `SpendingCap`, inside `CharacterRun` | 200,000 tokens, as today |
   | Per person | The API's monthly cap | $2 a calendar month, in UTC |
   | In total | The same | $10 a calendar month, in UTC |

   The monthly cap is a `DelegatingChatClient` in `NpcForge.Api`, around the model client the run is
   given. Before each call it reads the person's spend and the site's for the month, and refuses once
   either has reached its limit. After each call it adds the cost, which is input tokens times the
   input price plus output tokens times the output price. Cached input counts at the full input price,
   which overcounts and errs safe. The two prices are settings beside the model setting, and the API
   refuses to start on a paid provider without them. The limits are settings too.

   It refuses by throwing `SpendingCapReachedException`, which gains a constructor that takes a
   message. The message says whose cap stopped the run and when it resets, for example "Your $2.00
   for November is used up. It resets on 1 December, UTC." The run turns any cap refusal into its
   `Stopped` outcome, so the page shows its cap state as now. As today, one call can overshoot. A
   person's cap can overshoot by one call for each of their interviews running at once, and the
   total by one call for each interview running anywhere on the site. The owner's own use of the
   site has the same $2 cap.

   The backstop is a separate OpenAI project and key for the site, with a hard limit of $15 a month,
   above the $10 total so the site's own cap stops first. The console keeps the owner's own key.

5. **The console keeps its file, and the owner's 16 saves are copied once.** Each save is loaded
   from the file store and saved through the Postgres store into the owner's shelf, in number order,
   so they keep the numbers 1 to 16 and their briefs byte for byte. The copy refuses a shelf that
   holds anything, so a second run changes nothing. It runs once, after the owner's first sign-in on
   the deployed site and with the owner's yes. Aspire's firewall rule lets only Azure reach the
   database, so the copy most likely runs inside the API. PR 11 decides how. While the copy uses
   `FileCharacterStore`, the store stays in `NpcForge.Adapters`. Console saves made later stay in the file. Under the AppHost the API uses the local
   Postgres container, so the local site no longer shows `characters.json`.

6. **The owner opens the accounts, and a merge to master deploys.** The owner opens an Azure
   subscription (a new free account, for the free year), the External ID tenant and its app
   registration, and the site's OpenAI project and key. The owner types each secret. No agent sees
   one.

   - **The first deploy is by hand.** An agent shows the `aspire deploy` command and runs it on the
     owner's Azure login after the owner's yes.
   - **The checks come first.** A PR before the restructures adds a GitHub Actions workflow that
     builds and runs the free .NET suite and the pages' suite on each PR, on `ubuntu-latest`,
     because the Postgres tests will need a Linux container. It needs no Azure, so every PR after it
     proves itself on Linux as well as on the owner's machine.
   - **The deploy on each merge comes after the first deploy.** On each push to master a second
     workflow runs the same checks, then `aspire deploy`.
   - **The deploy signs in to Azure with a federated credential** that Azure trusts for this
     repository's `production` environment, so no Azure secret is stored in GitHub. The OpenAI key
     and the External ID secret are that environment's secrets, passed to `aspire deploy` as
     parameters on every run. GitHub gives no secret to a PR from a fork.
   - **Nothing from a deploy is cached.** Aspire's deployment state holds parameter values in plain
     text, secrets included, and a cache saved on master can be restored by other branches' runs.
   - **Only `master` may deploy to `production`.** The environment asks for no reviewer, so its
     branch rule is the lock. The owner's merge is the go.
   - **The site's key reaches the container as a secret parameter in publish mode only.** Local
     runs keep reading the console's user secrets, so the API keeps its `UserSecretsId`.
   - **The owner installs the Azure CLI and brings Aspire's CLI to 13.6.5** before the first deploy.
     ADR-020 turned Aspire's CLI bundle off for `dotnet run`, and PR 9 checks that `aspire deploy`
     works with that setting.

7. **`CharacterRun` becomes one interview's run.** It holds the model client inside its own
   `SpendingCap`, one `ChatOptions` from `ModelOptions.New`, one `Dice` and the run's report
   (decision 8). It lives across every roll again, so the next roll takes the next dice.

   ```csharp
   public sealed class CharacterRun(IChatClient model, string provider, string modelId, int? seed, IRunReport report)
   {
       public Task<Read> ReadAsync(string scene, CancellationToken ct);                               // the API: Drafted(SceneDraft) or Stopped
       public Task<Interviewed> InterviewAsync(TextReader gm, TextWriter screen, CancellationToken ct); // the console: Told, NotYes or Stopped
       public Task<Rolled> RollAndWriteAsync(Ask ask, CancellationToken ct);                          // Written(Draft), NoName, Refused or Stopped
       public static Task<int> SaveAsync(ICharacterStore store, Draft draft, CancellationToken ct);   // unchanged; the store is one person's
       public static IToolSource WriterTools { get; }                                                  // unchanged
   }
   ```

   - **Each outcome is a closed record** with one case for each way a step ends, so a front end
     handles every ending and no stop crosses the seam as an exception. A fault stays an exception:
     a dropped connection, a missing key, the offline stop.
   - **The run puts its token cap outermost**, around whatever client it is given, so a refused call
     is still never recorded or sent.
   - **`Ask` no longer names the provider**, which the run holds.
   - **`SceneInterview.RunAsync` and `ReadAsync` keep their signatures in this PR.** The run calls
     them, so `SceneInterviewTests` does not change here. Decision 8 then gives them the report. `InterviewAsync` is there until the console's dialogue
     moves out of the core (the review's third candidate), when the console calls `ReadAsync` and
     asks its own questions.
   - **`Program.cs` still reads in the order things happen**: connect, the run (which prints the dice
     line), the interview, the roll, the keep question, the save. ADR-014's rule holds. The lab keeps
     its own wiring, because only the console uses it.
   - **`Forge`'s interview holds its run, its draft, its owner and its gate.** The gate stays,
     because the run is no safer across threads than the dice are.

8. **A run reports through one seam, and the page hears its steps.**

   ```csharp
   public interface IRunReport
   {
       void Trace(string line);   // a trace line, as the core writes it today
       void Step(RunStep step);   // what the page shows while it waits
   }

   public enum RunStep { Rolling, Naming, Writing }
   ```

   - **The run hands its report to everything that traces.** The namer, the writer, the loop, the
     tools and the interview all get it, and all 12 trace lines go through it, with the dice line the
     run now prints. Every member that writes a trace takes the report, so its test call sites
     change, `SceneInterviewTests` included. The core then writes
     nothing to the console, and `BannedSymbols.txt` gains `Console.Error`.
   - **The console's report writes each line to stderr as today, byte for byte**, so the replay and
     every line in `docs/runs/` stay the same.
   - **The API's report logs each line through `ILogger` under the interview's id**, and passes the
     steps to the roll's answer.
   - **The tests' report keeps a list.**
   - **The report is not a port.** It is a seam inside the process, as `IToolSource` is, and the
     core gains no reference.
   - **The lab's blind mode can pass a report that drops every line** instead of swapping
     `Console.Error` for the whole process. Whether `LabRunTests` can then run beside the others is
     for the build to find.

   The roll's answer is a stream. `POST /api/interviews/{id}/characters` answers
   `text/event-stream` through `TypedResults.ServerSentEvents`, with one `step` event for each step
   and then one event with today's body (`written`, `no-name` or `stopped`).

   - **Faults keep their statuses until the first event.** A 404, a 409 or a 503 found before then
     is still its status. A fault after it arrives as a `failed` event with the same problem
     details, and `api.ts` throws the same `ApiError` for both. A roll the dice refuse ends after the
     rolling step, so today's 422 always arrives as a `failed` event.
   - **`rollAndWrite(interview, onStep)` gains the step callback**, and the wait screen marks the
     step.
   - **Closing the tab cancels the request**, and with it the roll. A model call already sent may
     still be billed, and the cap counts only the answers it receives.
   - **Starting an interview stays one JSON answer.** It is one model loop with no steps inside it.
   - **The Vite proxy and Container Apps' ingress must pass the stream on as it comes.** The PR that
     streams checks both.

9. **The tests.**
   - **`CharacterStoreTests` runs on the Postgres store too**, against a Postgres 16 container that
     the tests start. Those tests carry `[Trait("Category", "Database")]` and fail, never skip, when
     Docker is not running. The fast part leaves them out, as it leaves out `Process`.
   - **Two saves at the same moment get different numbers on Postgres.** The saves come from real
     threads, because a test that only awaits two tasks could run them in turn (ADR-019 decision 10).
   - **Two people never see each other's saves.** A test signs in as two people through the stand-in
     sign-in and fails if either can list, load, save into or roll on the other's things. It is shown
     failing with the owner filter removed.
   - **A capped run calls no model.** A test shows a run refused, with no model call, once a person's
     cap or the total cap is reached, on the stand-in with prices set.
   - **An uninvited sign-in is refused**, and nothing is stored for it.
   - **The stand-ins cannot reach the deployed site.** One test shows the stand-in sign-in refused
     with a paid model, and one shows either stand-in refused when the Container Apps variable is set.
   - **The steps come before the result.** A test reads the stream's step events before its result.
   - **The API's other tests stay in the fast part**, on `InMemoryCharacterStore` for each person,
     put in through the test host as the stand-in model is. One owner-filter test runs on Postgres.
   - **The browser tests run on Postgres.** Playwright starts a Postgres container for its API, on
     the stand-in model and the stand-in sign-in, and removes the container itself when it ends,
     because stopping `docker run` on Windows leaves the container running. A browser test signs in, makes a character, signs
     out, signs in again and finds it.
   - **The deployed check is the owner's.** A script the owner runs signs in on the deployed site as a
     test account the owner made, and lists its saves with no model call. No agent types that
     account's password.
   - **The replay and roll-again tests pass on the unchanged fixtures.**

10. **Eleven PRs, in this order.** A restructure and a behavior change never share a PR (ROADMAP.md).

    | # | PR | Kind | Its check |
    | --- | --- | --- | --- |
    | 1 | This record | Docs | The owner's review |
    | 2 | GitHub Actions checks on each PR, on Linux | Tooling | A PR shows its checks, and the free suites pass on Linux |
    | 3 | `CharacterRun` as the interview's run | Restructure | Replay and roll-again unchanged. The equality test compares two runs built the same way |
    | 4 | The run's report. The trace lines through `IRunReport`, and `Console.Error` banned in the core | Restructure | Replay and roll-again unchanged, and a recorded run's stderr identical |
    | 5 | The Postgres store, the AppHost's Postgres, and the API on it | Behavior | `CharacterStoreTests` and the numbering test on Postgres |
    | 6 | Sign-in. External ID, the invite list, a store per person, interview owners, the stand-in sign-in and its two locks, the keys in the database, the pages' sign-in states | Behavior | The two-person, uninvited and refusal tests, each guard shown failing. The browser test. The owner's sign-in (*needs the owner's eyes*) |
    | 7 | The monthly caps in dollars | Behavior | The cap tests, with no model call |
    | 8 | The roll as a stream, and the wait screen's steps | Behavior | The stream test and the browser test. The steps' wording (*needs the owner's eyes*) |
    | 9 | The first deploy, by hand. The Container Apps environment, the pages in the API's container, one copy at most, the owner's guide to the accounts and the tools | Behavior | The owner's deployed check |
    | 10 | GitHub Actions deploys each merge to master | Behavior | A merge deploys, and a branch other than master cannot |
    | 11 | The copy of the owner's 16 saves, the first paid run by a friend, the close-out | Behavior | The copy's test. The friend's run, and the OpenAI billing page after a week (*needs the owner's eyes*) |

## Why

- **Azure, Postgres and External ID.** The owner wants the practice. Within that, Flexible Server
  is the route Aspire is built for. The AppHost names the database once for both modes, and no
  database password exists.
- **The API does the sign-in.** Microsoft holds the passwords, and the page only gets a cookie on
  its own address. So a page changes only where a sign-in forces it, as the epic asks
  (principle-experience-first).
- **The invite rule is code, not the tenant's setting.** It runs in the free tests, so the epic's
  refusal check proves it (principle-prove-it-works).
- **Caps in dollars.** The goal is "before it costs me more than a cap I set". Each cap has one
  home. The token cap lives in the run, and the money lives in the API, so the core learns nothing
  of months, prices or people (principle-laziness-protocol).
- **One run object.** The cap, the dice and the options were built twice, and epic 6 changes the
  cap next. A closed outcome makes every way a step ends visible in its type.
- **One report seam.** Friends' runs get logs that can be told apart, and the page gets the steps
  that really happen rather than a guessed animation (principle-experience-first).
- **The checks, then the restructures, first.** PR 2 makes every later PR prove itself on Linux.
  PRs 3 and 4 move code with the replay as the proof, so the behavior PRs build on a base that is
  known to be good (principle-sequence-verifiable-units).
- **One deploy by hand, then the pipeline.** The first deploy is where Azure's surprises turn up.
  The pipeline then automates a deploy that is known to work.

## Left to the owner

Each has a default, so the build can start.

- **The friends' model.** Default: gpt-6-luna, the console's default, through the API's model
  setting and its two prices. Settle it before a friend's first paid run. A new model needs its
  prices changed with it.
- **Whether the API stays running when idle.** Default: one copy stays running. Scaling to zero is
  cheaper, but a restart then forgets every interview and draft left idle past the scale-in delay
  (about five minutes, not checked). A friend who reads a sheet that long and then saves gets a
  404, and the character is lost. PR 9 measures what one running copy costs, which I guess at a few
  dollars a month.
- **The wait screen's words for each step.** *Needs the owner's eyes.*
- **Turning off sign-up for strangers in the tenant.** Default: not set. The invite list is the
  rule.

## Consequences

- **AGENTS.md changes in the PR that makes each line true.**
  - In PR 2, "Build and run" says that each PR runs the free suites on GitHub.
  - In PR 4, "Dice, the clock, traces and skill text are not ports" says that traces go through the
    run's report, a seam as `IToolSource` is. The core's rule becomes "The core never writes to the
    console or reads stdin".
  - In PR 5, "Build and run" says that `dotnet test`, `npm test` and the AppHost need Docker
    running, that the first run pulls the Postgres image, and that the fast part is
    `--filter "Category!=Process&Category!=Database"`. "My saves" says that `characters.json` is
    the console's, and that the site's saves are in Postgres.
  - In PR 6, the free run sets the stand-in sign-in.
  - In PR 9 and PR 10, "Ceremony matches risk" gains deploying. Until PR 10, an agent asks before
    each deploy, with the command. From PR 10, a merge to master deploys.
- **ADR-014, amended.** Decision 3's static `CharacterRun` becomes an object, `Ask` loses its
  provider, and the members that trace gain the report. Decision 4's traces go through the run's
  report, not `Console.Error`, and the API's report is where `ILogger` comes in. Decision 7's
  "`Console.Error` stays allowed" ends, because the core no longer writes to the console. In the Consequences, "Each friend gets a
  `SpendingCap`" becomes the three caps in decision 4. "Traces stay on `Console.Error`" no longer
  holds after PR 3, and PR #86 retired "`ChatOptions` is changed by `AgentLoop`".
- **ADR-016, amended.** In decision 4, the run prints the dice line, and `Program.cs` handles the
  run's `Refused` outcome where it caught `RollRefusedException`.
- **ADR-017, amended.** Decision 2's `npm test` also needs Docker. In decision 3, `rollAndWrite`
  gains its step callback.
- **ADR-019, amended.**
  - Its context's "No Docker was needed" no longer holds, because the AppHost starts Postgres.
  - Decision 2's "no new type" gives way to `CharacterRun`. `ReadAsync` and the four members on
    `SceneDraft` stay.
  - In decision 3, an interview holds its run and its owner, and the run builds the options.
  - Decision 5's site shows each person's saves from Postgres, not the owner's file.
  - Decision 6's cap for each interview gains the two monthly caps.
  - In decision 8, `rollAndWrite`'s signature changes.
  - Decision 9's AppHost gains Postgres, the Container Apps environment and
    `PublishWithContainerFiles`.
  - Decision 4's fault table holds until the roll's first event. After it, a fault is a `failed`
    event.
  - Decision 7's stand-in model gains the stand-in sign-in, and neither starts in Container Apps.
  - In decision 10, the API's tests use an in-memory store for each person, and the browser tests
    a Postgres container.
  - Its consequences "The API's log mixes two interviews at once" and "Epic 6 inherits" are met
    here. Its consequence that an agent runs the AppHost with `NPCFORGE_CHARACTERS` set no longer
    holds for the API, which reads Postgres.
- **The epic's open choices are answered here**, except the model, which stays under "Left to the
  owner".
- **Running costs.** About $5 a month for the first year on a new free account, mostly the
  registry, then about $20 to $25 a month for the database and the registry, an estimate. Model
  spend stops at the caps.
- **The site calls a paid model from its first deploy.** The API refuses the stand-in model in
  Container Apps, so the first deploy carries the site's key, behind the three caps and the hard
  limit. That changes ROADMAP.md's task order, which deploys before a paid model is switched on.
- **Two items from `docs/design/epic-12-api-needs.md` stay as they are.** The top bar's count still
  costs a list, and a choice the page's table does not know is still shown as sent.
- **The tests have never run off the owner's machine.** PR 2's first runs on Linux may find
  something that works only on Windows.
- **A restart still forgets every interview**, and so does scaling to zero. The page shows its
  failure state with the scene kept, as now.
- **The verify skill changes with PRs 5 and 6.** `api start` and `apphost start` need Docker and the
  stand-in sign-in.
- **The console does not change for the owner**, apart from what PR 3 and PR 4 move behind the
  same bytes.
