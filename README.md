# NpcForge

Writes non-player characters for tabletop roleplaying games. You describe the scene, dice settle
who the character is, and a model writes them: who they are, where they stand, what would move
them, and what they do after the players leave.

A .NET 10 console app and an MCP server. It is also where I practise directing AI agents well;
[AGENTS.md](AGENTS.md) says how work is done here.

## Why

A chat window is a poor place to write game dialogue, for two reasons:

- **The model never has enough context.** The setting, the situation and the character have to be
  retyped every time, so they arrive thin and inconsistent.
- **The model invents the same people over and over.** Ask it for an interesting trait and you get
  one of about four answers, forever.

Better prompting fixes neither. The facts need to live where the app can look them up, and the
random part needs to happen in code, where the model cannot reach it.

**The core principle: the model never guesses. It is either told or it rolls.** The model does the
writing. The dice give the variety.

## How it works

1. You give four answers: where we are, what the players want, how hard it should be, and who the
   character is (an innkeeper by default).
2. The app has the server roll up a **character brief**: what the character wants, what kind of
   obstacle they are, an attitude, a mannerism, a weakness, something they will not talk about,
   and one thing they are wrong about. The rolls stay inside what the difficulty allows, so a wall
   does not come out cheerfully helpful.
3. The model gets the brief as settled fact, plus writing guidance, and writes the character in a
   fixed order:
   - **Who they are.** A name and one line of history.
   - **Where they stand** on what the players want.
   - **How they seem, and what is underneath.**
   - **Levers.** Two or three things that would move them, each with a line of dialogue.
   - **Anti-levers.** Two or three things that would harden them, each with a line.
   - **Afterwards.** What they do if the players got what they wanted, did not, or made an enemy.
4. Every run is saved, brief and text together, and numbered. `--load <number>` prints one back.

**Levers come from the brief.** Left alone, a model offers bribe, persuade and intimidate in every
scene. Tied to the brief, they get specific: a man who will not talk because he is afraid of being
seen to take a side can be moved by being offered cover, or a version of events he can deny later.

## Difficulty

Difficulty is the friction between two wants, the players' and the character's.

- **Easy.** The character still asks for something, but almost any reasonable attempt clears it.
- **Some work.** The ask is real. A wrong approach can fail.
- **A wall.** The character's want runs directly against the players'.

A second roll stops every hard character coming out stubborn: they **won't** (distrust, fear,
loyalty to someone else), they **can't** (they do not know, or may not say), or they will **for a
price** (coin, a favour, a problem solved first). Three different scenes at the same difficulty.

## Run it

Needs the .NET 10 SDK and an OpenAI or Anthropic API key.

```
dotnet user-secrets set "OpenAI:ApiKey" "<your key>" --project NpcForge.Console
dotnet build
dotnet test
dotnet run --project NpcForge.Console
dotnet run --project NpcForge.Console -- --setting "an isolated farm on a dangerous border" --occupation farmer --want "passage" --difficulty SomeWork
dotnet run --project NpcForge.Console -- --load 1
```

Flags: `--provider openai|anthropic`, `--model <id>`, `--setting`, `--want`,
`--difficulty Easy|SomeWork|Wall`, `--occupation`, `--load <number>`, and for the free replay
`--seed`, `--record`, `--recording` and `--provider replay`.

With no answer flag (`--setting`, `--want`, `--difficulty`, `--occupation`) and no `--load`, the app
greets you and waits for the scene, typed in one go and ended with Enter. A model reads it and
records only what your words say. You then see every field with its label: *my words* (a span you
typed), *inferred* (a difficulty or obstacle read from your words, shown beside them), *rolled* (the
dice pick it) or *default* (a required field you did not mention). Nothing is rolled until you type
`yes`; anything else stops the run. Any answer flag skips the interview, and each answer flag has a
default. A flag the app does not know, or one with no value, stops the run before anything is paid for.
A run calls a real model and costs money, capped at 200,000 tokens per session; `dotnet test` and
`--load` do not. `NPCFORGE_OFFLINE=1`
makes the app refuse OpenAI and Anthropic outright. A replay or a recording run saves to a temp file,
named on its `[app] saved as` line, so it never touches `characters.json`.

The character is printed on stdout. A trace on stderr shows each step: `[app]` the rolled brief,
`[skill]` the writing guidance used, `[loop]` each model turn, `[tool]` and `[server]` each tool
call. The interview's greeting, confirmation and question are on stderr too, so stdout stays the
character alone.

## Status

Works end to end on OpenAI and Anthropic. The success test has passed: the same answers, three
runs, three genuinely different characters whose levers differ, and a saved one loaded back
unchanged. The Safety net is in and hardened: `dotnet test` replays the whole app against one
recorded run, for free, so a later change can be checked without paying. A mistyped flag cannot start
a paid run, `NPCFORGE_OFFLINE=1` stops a free run reaching a model, a replay never writes over the
saved characters, and `dotnet test -c Release` tests the Release build. Talking to the app is
built and tested free: a bare `dotnet run` interviews you about the scene, and every trait traces to
your words or the dice. Its paid run on OpenAI and Anthropic is still to come. The writer is now
sent your scene as well as the brief, so the character acts inside the situation you described. Next: better characters (tone, names that don't repeat), keeping and editing
characters, and in time a website with logins and a database so friends can use it. See
[ROADMAP.md](ROADMAP.md).

## Out of scope, for now

- Anything tied to a specific campaign, factions and their politics included
- Tracking how a character feels about the players over time
- Use during a live session

The app has no world to ask about. Local trouble ("does not deal with the thieves' guild") has to
arrive with the setting, or the model invents it. That is the seam where a real campaign would
plug in later.

## More

- [AGENTS.md](AGENTS.md): how work is done here
- [ROADMAP.md](ROADMAP.md): what is next
- [docs/architecture.md](docs/architecture.md): how it fits together
- [docs/decisions/](docs/decisions/INDEX.md): why it is shaped this way
