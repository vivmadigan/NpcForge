// The bakery run, docs/runs/raw/2026-11-08-01-epic4-bakery-roll-again.md, as typed values for the
// named states: its scene, the seven fields its scene settled in the order the roll lists them, and
// the first character it wrote. The scene and the character were cut from the run file by a script,
// not typed: the strings are JSON-escaped so no editor can trim the lines that end in two spaces,
// which Markdown reads as a line break. tests/states.spec.ts holds the sheet to the run file. The
// market scene and its three entries below are from a second run, whose scene says less and gives a
// want and no gender. No page imports this file; only the named states do (ADR-017 decision 3).
import type { ConfirmEntry } from '../api';

export const bakeryScene: string = "The party bursts into a tiny bakery where a goblin baker is furious that someone keeps stealing his croissants. It’s a silly session, play it for laughs.";

// What the bakery run's scene settled: seven entries in the roll's order, with the values the brief
// holds, as the run's `[app] brief` line spells them. The run's other 12 fields were rolled after the yes.
export const bakeryFields: ConfirmEntry[] = [
  { field: 'setting', value: 'tiny bakery' },
  { field: 'playersWant', value: null },
  { field: 'occupation', value: 'baker' },
  { field: 'attitude', value: 'furious' },
  { field: 'tone', value: 'Comic' },
  { field: 'race', value: 'goblin' },
  { field: 'gender', value: 'Male' },
];

// The scene that says less, from docs/runs/raw/2026-11-03-01-epic10-interview-nogender.md: a place, a
// want and a role, and no gender. The scene is the sentence that run's confirmation quoted from, and
// the entries are its three `[my words]` fields. Only the named state forge-confirm-market opens it.
export const marketScene: string = "The party walks into a crowded market and asks the spice seller for the fastest road to the capital.";

export const marketFields: ConfirmEntry[] = [
  { field: 'setting', value: 'crowded market' },
  { field: 'playersWant', value: 'the fastest road to the capital' },
  { field: 'occupation', value: 'spice seller' },
];

// The bakery run's first character, which the named state forge-written shows.
export const bakeryCharacter: { name: string; sheet: string } = {
  "name": "AldoBun Sourdough",
  "sheet": "## Their name\nAldoBun Sourdough\n\n## Read it to the table\n> This is a tiny bakery.\n> An old male goblin with a big nose stands behind a tray of croissants.\n> He counts them furiously, loses count, and starts again with a scowl.\n> Before you can speak, he says, “Welcome! Please don’t all talk at once; I can interrupt you individually.”\n\n## Who they are\nAldoBun once wrote a formal complaint to a croissant for looking suspiciously flaky.\n\n## Where they stand\nYou’re unexpected visitors, and AldoBun is too furious about the repeated croissant theft to be welcoming. He wants you to find [who keeps stealing the croissants]. Alongside that, he wants to get out of [the unpleasant responsibility]; he’ll offer real cooperation and goodwill if you agree to take it over.\n\n## How they seem, and what is underneath\nHe comes across as furious, quick to cut in, and absolutely certain that “the proof is in the pudding” means pudding ought to come with paperwork. Underneath the bluster, a polite request is hard for him to refuse—but his cooperation has a price: take [the unpleasant responsibility] off his hands.\n\n## Levers\n- **Polite request (weakness):** Ask nicely, and he finds it hard to say no. You earn a small tip or a more helpful answer.  \n  “Oh, you said *please*. Now I have to help. It’s in the pudding paperwork.”\n\n- **Agree to take [the unpleasant responsibility] (characterWants; obstacle: ForAPrice):** That’s his price. He warms to you and gives you his cooperation.  \n  “You’ll take it? Wonderful. I’ll put your name in the pudding as proof.”\n\n- **Offer to investigate the croissant theft (scene):** He gives you the job and a chance to earn his gratitude by finding [who keeps stealing the croissants].  \n  “Find the thief, and I’ll give you my full support—after I notarize it inside a pudding.”\n\n## Anti-levers\n- **Refuse his price (characterWants; obstacle: ForAPrice):** He won’t give you his goodwill for free; you’re left with his bare minimum help.  \n  “No responsibility, no extra help. That’s not a threat; it’s a very small contract.”\n\n- **Ask about the game he recently lost (willNotDiscuss):** He shuts the subject down and offers no more than he has to.  \n  “That game is closed. Please direct all questions to the pudding.”\n\n- **Make a demand without asking politely (weakness: finds it hard to refuse a polite request):** You miss the one easy way to soften him, and his temper holds.  \n  “That wasn’t a please. I can’t file that under ‘please.’ The pudding would reject it.”\n\n## Afterwards\nIf you take the responsibility, he leaves won over and tells everyone the pudding witnessed the agreement. If you don’t, he’s left as he was, still counting croissants. If you mock his paperwork or press him about the game, he makes you an enemy—and gives the pudding your description."
};
