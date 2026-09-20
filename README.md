# NPC Dialogue Forge

A console app that writes non-player characters and their dialogue for
tabletop roleplaying games. Built mainly to learn how agent loops, Skills and
MCP fit together.

**Status:** proof of concept. Nothing built yet.

## Why this exists

A chat window is a poor place to write game dialogue. Two things go wrong.

**The model never has enough context.** The setting, the situation and the
character all have to be retyped every time, so they arrive thin and
inconsistent.

**The model invents the same people over and over.** Ask it for an interesting
personality trait and you get one of about four answers, forever. Models are
weak at open-ended invention.

Neither is fixed by better prompting. The first needs the facts stored
somewhere the app can look them up. The second needs the random part produced
in code, where the model cannot reach it.

## Core principle

**The model never guesses. It is either told or it rolls.**

The model's job is the writing. The variety comes from dice.

## The three questions

You set up a scene by answering three questions. All three are things you
already know while prepping.

| Question | Example answers |
| --- | --- |
| Where are we? | An inn in a major city. An isolated farm on a dangerous border. |
| What do the players want? | A job. Information. Passage. A name. |
| How hard should it be? | Easy. Some work. A wall. |

Who the character turns out to be is not one of the questions. That is the
app's job, and the third question is what steers it.

## How difficulty works

Difficulty is friction between two wants. The players want something. The
character wants something of their own. How far those two pull apart is the
difficulty, so setting it is really setting what the character wants.

- **Easy.** What the players want costs the character nothing to give. They
  may still want something of their own, but it does not get in the way.
- **Some work.** The two wants pull against each other. There is a price, or
  a hesitation to talk past.
- **A wall.** The character's want runs directly against the players'.

Difficulty on its own is too flat, though. Left there, every hard character
comes out as a variation on stubborn. So the app also rolls for what kind of
hard it is:

- **They won't.** Distrust, fear, or loyalty to someone else.
- **They can't.** They do not actually know, or they are not allowed to say.
- **They will, for a price.** Coin, a favour, or a problem solved first.

Three completely different scenes at the same difficulty.

## The character brief

Before any writing happens, the character is reduced to a short list of
traits. Call that list the brief, in the sense of a brief you hand a writer.

It is your three answers, plus what the app rolls up:

- what this character wants from the players
- what kind of obstacle they are: won't, can't, or for a price
- an attitude, such as greedy, rushed off their feet, distrustful, frightened
- a mannerism
- a weakness
- something they will not talk about
- one thing they are wrong about

The rolls are held inside what the difficulty allows, so a wall does not come
out cheerfully helpful.

Anything on that list can be set by hand instead. When you already know she is
greedy, say so and the app rolls the rest around it.

The brief is settled first and does not change. Everything written about the
character comes from it.

## What comes back

One character, in a fixed order, every time.

**Who they are.** A name and a single line of history. A line, not a
backstory. "Has run this place for a decade and knows every face in it."

**Where they stand.** What they know about what the players want, and their
position on it. Often the most useful line on the page.

**How they seem, and what is underneath.** The surface manner, then the real
state of things. A friendly innkeeper who turns out to be badly worried. This
costs nothing to produce, because the attitude and the reason behind the
obstacle are already two separate entries in the brief.

**Levers.** Two or three things that would actually move this person, each
with a line of dialogue for the moment it lands.

**Anti-levers.** Two or three things that would harden them, each with a line.

**Afterwards.** What this character does once the players walk out.

### Levers have to come from the brief

Left to invent freely, the model will give you bribe, persuade and intimidate
every time, in every scene, forever.

Tied to the brief, they get specific. A man who will not talk because he is
afraid of being seen to take a side can be moved by being convinced he is
already involved, by being offered cover, or by being given a version of
events he can deny later. None of those is a generic approach. All of them
fall out of his reason for refusing.

Anti-levers work the same way and fail the same way. Saying his name loudly in
a crowded taproom is a real anti-lever for that man. "Being rude to him" is
not an anti-lever, it is a placeholder.

### Afterwards

The part that gives a one-off scene a consequence. Three endings, one or two
actions under each:

- The players got what they wanted
- They did not
- They walked away having made an enemy

The frightened innkeeper who gets leaned on hard tells somebody about it. That
is next session's hook, and it came out of the same brief that produced the
refusal. It is also the first thing worth saving when a character turns out to
be worth keeping.

## Architecture

Three parts, kept separate on purpose.

**The console app** asks the three questions, rolls up the character, and then
runs the back and forth with the model. Because the rolling happens first, the
brief arrives as settled fact. The model cannot skip it or quietly pick its own
traits instead.

**Skills** are the writing guidance. The fixed order above, what a difficulty
obliges the writer to do, how to turn a trait into something the character
does, and the rule that levers trace back to the brief. A gambler should be shown rolling a knucklebone while he talks,
never saying "I have a gambling problem." The brief is raw material for the
writer. The character never announces it.

**The MCP server** holds the reference material and does the rolling. What a
typical innkeeper or farmer is like, the trait tables, the code that rolls on
them within a difficulty, and any character worth keeping. Each tool it offers
does one narrow job. The stored data stays on this side and never passes
through the model.

### Which part does a new idea belong to?

Could code produce it? Server. Does it only make sense as an instruction to a
writer? Skill.

A table of traits is code's work. "Show the weakness, never state it" is not a
fact to look up, and not something code can carry out. Anything that resists
the question is usually two ideas under one name, one for each side.

## Build order

Each step proves one thing. Do not start the next until the current one works.

**1. The loop.** One hardcoded innkeeper, one fake tool, no server, no Skills.
Proves the app can ask the model something, handle a request for a tool, feed
the answer back, and stop cleanly. Put a limit on how many times round it can
go.

**2. The server and the rolls.** Trait tables behind MCP tools, with the
difficulty deciding which entries are in play. The app rolls the character up
before the model's first turn and hands the brief over. The model now writes
from traits it did not choose.

**3. Skills.** Shape what comes back into the order above.

**4. Saving.** Store a character worth keeping, along with the brief that
produced them, and load them again by name.

The server comes before Skills on purpose. The thing being tested is whether
rolling traits beats asking the model for them. Add Skills first and there is
no way to tell which change made the difference.

## Success test

Same three answers. Three runs. Three genuinely different innkeepers, all of
them the difficulty that was asked for. Then load the second one back
unchanged.

Pass and the extra machinery earned its place. Fail and this is prompting with
more steps.

"Was the innkeeper any good" is not the test. A plain chat window already
passes that. The sharper version is whether the levers differ between the
three, since generic levers are the clearest sign the brief is being ignored.

## Out of scope

Left out on purpose, for now:

- Anything tied to a specific campaign, factions and their politics included
- Tracking how a character feels about the players over time
- Use during a live session, and any requirement to be fast
- Any interface beyond the console
- A real database. The only thing that needs saving is characters, so a file
  or SQLite is enough.

## Known gap

The app has no world to ask about. A line like "does not deal with the thieves
guilds" is local knowledge, so either the answer to "where are we" carries the
local trouble with it, or the model invents a faction, which is guessing.

Living with it is fine for a proof of concept. It is also the exact seam where
a real campaign plugs in later.

## Notes toward later

A generic innkeeper and a member of a specific faction work the same way:
shared traits and a shared voice, overridden per person. Pointing this at a
real campaign later means replacing the content, not rebuilding the app.

Two things worth keeping in mind even though they are out of scope now. First,
saving which of the three endings actually happened, so the character stays
consistent the next time the players meet them. Second, a way to take a
throwaway character the players ended up liking and promote them into one that
is properly tracked.
