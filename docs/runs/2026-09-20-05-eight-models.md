# Run 05 — 2026-09-20 — eight runs, four models

| | |
| --- | --- |
| Models | `claude-sonnet-5` ×2, `claude-opus-5` ×2, `gpt-5.6-luna` ×2, `gpt-5.6-sol` ×2 |
| Setting / want / difficulty / occupation | unchanged defaults — an inn in a major city, the name of a fence, `Wall`, innkeeper |
| Skill | `skills/npc-writer/SKILL.md`, 2097 chars |
| Commit | `469b6ba`, no code uncommitted |
| Raw captures | [`raw/2026-09-20-05-*.md`](raw/) — full console for each, via `cmd /c "... > file 2>&1"` |

Run from a terminal by Claude at the user's request, eight paid runs back to back. The only
variable is the model; the skill, the code and the four answers are identical throughout.
Compare against runs [02](2026-09-20-02-first-skill.md), [03](2026-09-20-03-for-a-price.md)
and [04](2026-09-20-04-cant.md), which are `gpt-5.6-terra`.

## What came back

| Run | Model | Obstacle | Character | Inn | Turns | Tool calls |
| --- | --- | --- | --- | --- | --- | --- |
| sonnet-1 | `claude-sonnet-5` | `ForAPrice` | Bartho Quill | the Crooked Lantern | 2 | 1 |
| sonnet-2 | `claude-sonnet-5` | `Wont` | Mira Colbeck | the Gilt Kettle | 2 | 1 |
| opus-1 | `claude-opus-5` | `ForAPrice` | Hessa Dunmore | the Sparrow & Pike | 2 | 1 |
| opus-2 | `claude-opus-5` | `Cant` | Halbrecht Onnow | the Pearl & Pennon | 2 | 1 |
| luna-1 | `gpt-5.6-luna` | `Wont` | Marda Pell | — | 2 | 1 |
| luna-2 | `gpt-5.6-luna` | `ForAPrice` | Marella Voss | the Bell-and-Basket | **1** | **0** |
| sol-1 | `gpt-5.6-sol` | `Cant` | Mara Venn | the Gilded Ladle | 2 | 1 |
| sol-2 | `gpt-5.6-sol` | `Wont` | Mara Pell | — | 2 | 1 |

## 1. The name convergence is an OpenAI-family trait, not a property of LLMs

Every OpenAI-family run in this project — all eight of them, across three different models —
has produced a first name beginning `Mar-` or `Mer-`:

> `gpt-5.6-terra`: **Mer**rin Voss · **Mar**a Venn · **Mar**a Vell · **Mar**a Venn
> `gpt-5.6-sol`: **Mar**a Venn · **Mar**a Pell
> `gpt-5.6-luna`: **Mar**da Pell · **Mar**ella Voss

The surnames recycle too: Venn ×3, Pell ×2, Voss ×2 — and *Voss* appears in both the very
first run (terra) and luna-2, two different models.

Anthropic, same brief, same skill: **Bartho Quill**, **Mira Colbeck**, **Hessa Dunmore**,
**Halbrecht Onnow**. Four names, no shared stem.

This sharpens the step 6 note considerably. The convergence was never "the model falls back on
favourites" in general — it is *this vendor's* favourites, and it survives across three model
sizes in the family. The README's argument for rolling traits holds either way, but the
evidence for it is vendor-shaped.

## 2. Anthropic converges too — on backstory, not on names

Both Sonnet runs gave the innkeeper **eleven years** at the inn and a **brother-in-law** in the
origin story:

> sonnet-1: *"ever since his brother-in-law drank the previous inn into debt and left him holding the deed"*
> sonnet-2: *"ever since she won it off her brother-in-law in a card game he still hasn't forgiven her for"*

Same stem, different spin. So the effect is not "OpenAI repeats itself and Anthropic does not" —
it is that each vendor has a different unrolled field it falls back on. Worth remembering before
concluding one model is more varied than another.

Inn names echo across vendors as well: terra gave the Brass **Lantern** and the Brass **Kettle**;
Sonnet gave the Crooked **Lantern** and the Gilt **Kettle**. Shared training priors for what an
inn is called, reached by different routes.

## 3. The skill's content contract held everywhere; its formatting did not

All eight runs produced all six sections, in order, with levers and anti-levers traced to brief
entries. But the shape differs by vendor:

- **OpenAI** (luna, sol, terra) follows the numbered list literally: `## 1. Who they are`.
- **Sonnet** drops the numbering for inline bold labels: `**Who they are:**`, `**Who she is.**`.
- **Opus** adds a title of its own and renames sections — opus-1 replaced "Who they are"
  with `**Keeper of the Sparrow & Pike, third generation, forty years old...**`.

Run 04 on terra had already shown *Afterwards* drifting from bullets to prose. Across vendors the
drift is wider. The six sections are reliable; their markup is not. If anything downstream ever
parses this output, that is the thing that will break it.

## 4. luna-2 never called the tool

`[loop] turn 1: 0 tool call(s), history 3, 682 in / 1165 out` — and that was the whole run.
Every other run of this project, on every model, has called `lookup_archetype` once. `gpt-5.6-luna`
decided it did not need to and wrote the character straight from the brief.

Nothing broke: the loop's exit condition is the absence of tool calls, so it returned on turn 1
exactly as designed. This is the first time the single-turn path has been seen against a real
model rather than in the step 4 fake-client test. It also makes the point that a tool the model
*can* see is a tool it *can* skip — which is the stated reason `roll_character` is kept off the
list entirely.

## 5. Obstacle coverage

`Wont` ×3, `ForAPrice` ×3, `Cant` ×2. Every model handled its draw correctly: the `Cant` runs
(opus-2, sol-1) had the character not know the name and redirect, the `ForAPrice` runs named a
price. The per-obstacle rule in `SKILL.md` is not an OpenAI-specific fix.
