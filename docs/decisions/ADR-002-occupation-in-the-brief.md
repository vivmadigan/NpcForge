# ADR-002: Occupation joins the character brief, supplied rather than rolled

- **Status:** accepted
- **Date:** 2026-09-19
- **Supersedes:** none
- **Plan version:** v01

## Context
Step 6 wired the server into the loop. Three live runs at the same answers (an inn in a major city, the name of a fence, `Wall`) gave two innkeepers and one inn patron, because nothing in the brief said who the character is — the model was choosing that on its own. The README's success test asks for "three genuinely different innkeepers", which needs the occupation held constant across runs. `CharacterBrief` did not have a field for it: README.md ("Who the character turns out to be is not one of the questions. That is the app's job") and BUILD.md's brief both leave it out.

## Options considered
- **A** Leave it out of the brief, as originally specified: the model keeps choosing who the character is, which is what produced the patron.
- **B** Roll it on the server from a table (`["innkeeper", "tavern owner"]`) with an optional override, like the other traits: the server receives the setting as free text and cannot tell which list applies, so a rolled default would put "innkeeper" into a farm's brief as settled fact — the first message tells the model the brief is settled and not to be changed. Two near-synonyms also add no real variety, so the "roll" would be a constant dressed as dice.
- **C** Supply it like the other "you supply" fields (`Setting`, `PlayersWant`, `Difficulty`): a required `Occupation` on `CharacterBrief`, defaulted to `"innkeeper"` in `Program.cs` next to the setting default, overridden with `--occupation`. Not rolled by the server.

## Decision
C.

## Why
The brief exists to settle facts the model should not be re-deciding; occupation turned out to be one of them, the same way setting and difficulty already are. Option B was weakened by the challenger's finding above — a rolled default cannot be setting-aware without the server parsing free text, and a two-item table is not meaningfully random. Supplying it costs nothing the app does not already pay for the other three "you supply" fields, and keeps the rolled/supplied split legible: rolled traits vary per run to give the model raw material, supplied fields hold the run's premise steady so the success test can actually be checked across repeats.

## Consequences
- Easier: repeat runs at the same setting now vary the character while holding "who they are" fixed, which is what the README's three-innkeepers test needs; the split between rolled and supplied fields stays consistent with `Setting`, `PlayersWant` and `Difficulty`.
- Harder: the brief now departs from README.md's three questions and from BUILD.md's brief as originally sketched; a farm run needs `--occupation` supplied alongside `--setting` or it silently gets an innkeeper default; the brief is step 8's save format, so saved characters carry the occupation going forward.
- Revisit if: a setting other than a settlement (the farm case) makes the `"innkeeper"` default actively wrong often enough that it needs its own default-selection logic, or if the model's name convergence (still open, noted under step 6 in `PLAN.md`) turns out to need the same treatment — a field added to stop the model from silently deciding something the brief should settle.
