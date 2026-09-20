# Run 06 — 2026-09-20 — the whole difficulty ladder, for the first time

| | |
| --- | --- |
| Model | openai / `gpt-5.6-terra` ×3 |
| Difficulty | **`Easy`, `SomeWork`, `Wall`** — one run each |
| Setting / want / occupation | unchanged defaults |
| Skill | `skills/npc-writer/SKILL.md`, **2239 chars, `6b58d7c2`** |
| Commit | the `Easy`/`SomeWork` rewrite, uncommitted at run time |
| Raw captures | [`raw/2026-09-20-06-*.md`](raw/) |

Two firsts. This is the first run of any kind at `Easy` or `SomeWork` — runs 01–05 were
`Wall` twelve times out of twelve — and the first with a content fingerprint in the
`[skill]` line rather than a character count alone.

## Why the skill changed first

The step-done review found that the `Easy` bullet said both *"it costs them nothing"* and
*"If there is a cost it is small"*. Nothing parses `SKILL.md`, so the model would have
resolved that silently, differently per run, with no signal to anyone. Rewriting it moved
both bullets onto the axis that actually matters — **not what it costs the character, but
how hard the ask is for the players to clear**:

> - **Easy** — the character still asks for something; nobody hands things to strangers for
>   free. But the ask is small, and almost any reasonable attempt clears it. Give the players
>   something to do, then let them succeed at it.
> - **SomeWork** — the ask is real. The players have to offer the right sort of thing, or talk
>   past a hesitation first. A wrong approach can fail.

## The three rungs now read differently

| Difficulty | The ask | Can the players fail it? |
| --- | --- | --- |
| `Easy` | Buy the ugly brass warming-pan Mira has been trying to shift | No — *"even a modest one"* clears it |
| `SomeWork` | Remove a man from the back room, quietly | Yes — a spectacle would not count |
| `Wall` | Give her a credible way to keep trouble off her name | Only a lever from the brief, and it costs her |

`Easy` is the one that was at risk, and it came out right. There **is** an ask, because nobody
hands things to strangers for free, and the lever says outright: *"Make a reasonable offer for
the unwanted pan, even a modest one."* That is the "as long as they kinda do, it's a pass"
quality the rewrite was aiming at, and it still produces a scene — which the old wording, read
as "it costs them nothing", would not have.

`SomeWork` earns its middle rung. *"There's a man in the back room who has mistaken my silence
for permission. See him out — quietly — and we can talk names."* A real task, and a wrong
approach fails it. The second lever confirms the direction: *"You asked proper. That puts you
ahead of most who come looking for Senn."*

## Observed

- **The fingerprint works.** All three runs read `2239 chars, 6b58d7c2`, the value computed
  from the committed file before the runs. The twelve earlier runs read `2097 chars` with no
  hash, so the two generations can never be confused. A same-length edit — swapping `Wont` for
  `Cant` in the obstacle bullet — gives `2239 chars, 70057667`: identical count, different
  hash. That is the failure the count alone could not see, and the reason the hash was added.
- **The model traced `difficulty` itself as a brief entry.** The `Easy` run's second lever is
  labelled `**difficulty: Easy**`. Nothing asked for that; the rule says levers name the brief
  entry they come from, and the model treated the difficulty as one. Harmless, arguably
  correct, and not seen in any earlier run.
- **The OpenAI name convergence continues, unbroken.** Mira Vell of the **Brass Kettle**, Mara
  Vell of the **Brass Cup**, Mara Vell of the **Brass Cup**. `Vell` for the third, fourth and
  fifth time; `Brass` in seven of the nine OpenAI-family inns that have been named at all. The
  `Brass Kettle` is an exact repeat of run 03. See the step 6 notes in `PLAN.md` for the
  corrected reading of this.
