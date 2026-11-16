// The bakery run, docs/runs/raw/2026-11-08-01-epic4-bakery-roll-again.md, as typed values: its
// scene, the seven fields its scene settled in the order the roll lists them, and the three
// characters it wrote, each with the brief from its `[app] brief` line in that line's order. The
// scene and the characters were cut from the run file by a script, not typed: the strings are
// JSON-escaped so no editor can trim the lines that end in two spaces, which Markdown reads as a line
// break. tests/flow.spec.ts holds each sheet to the run file. The market scene and its three entries
// below are from a second run, whose scene says less and gives a want and no gender.
// No page imports from madeUp/ (ADR-017 decision 3).
import type { BriefEntry, ConfirmEntry } from '../api';

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

export const bakeryCharacters: { name: string; brief: BriefEntry[]; sheet: string }[] = [
  {
    "name": "AldoBun Sourdough",
    "brief": [
      {
        "field": "setting",
        "value": "tiny bakery"
      },
      {
        "field": "playersWant",
        "value": null
      },
      {
        "field": "difficulty",
        "value": "SomeWork"
      },
      {
        "field": "occupation",
        "value": "baker"
      },
      {
        "field": "characterWants",
        "value": "to get out of an unpleasant responsibility"
      },
      {
        "field": "obstacle",
        "value": "ForAPrice"
      },
      {
        "field": "attitude",
        "value": "furious"
      },
      {
        "field": "mannerism",
        "value": "answers before others finish speaking"
      },
      {
        "field": "weakness",
        "value": "finds it hard to refuse a polite request"
      },
      {
        "field": "willNotDiscuss",
        "value": "a game they recently lost"
      },
      {
        "field": "wrongAbout",
        "value": "misunderstands a common saying and keeps using it"
      },
      {
        "field": "tone",
        "value": "Comic"
      },
      {
        "field": "race",
        "value": "goblin"
      },
      {
        "field": "nameLetter",
        "value": "A"
      },
      {
        "field": "nameSyllables",
        "value": "3"
      },
      {
        "field": "familyLetter",
        "value": "S"
      },
      {
        "field": "gender",
        "value": "Male"
      },
      {
        "field": "age",
        "value": "Old"
      },
      {
        "field": "look",
        "value": "big nose or ears"
      }
    ],
    "sheet": "## Their name\nAldoBun Sourdough\n\n## Read it to the table\n> This is a tiny bakery.\n> An old male goblin with a big nose stands behind a tray of croissants.\n> He counts them furiously, loses count, and starts again with a scowl.\n> Before you can speak, he says, “Welcome! Please don’t all talk at once; I can interrupt you individually.”\n\n## Who they are\nAldoBun once wrote a formal complaint to a croissant for looking suspiciously flaky.\n\n## Where they stand\nYou’re unexpected visitors, and AldoBun is too furious about the repeated croissant theft to be welcoming. He wants you to find [who keeps stealing the croissants]. Alongside that, he wants to get out of [the unpleasant responsibility]; he’ll offer real cooperation and goodwill if you agree to take it over.\n\n## How they seem, and what is underneath\nHe comes across as furious, quick to cut in, and absolutely certain that “the proof is in the pudding” means pudding ought to come with paperwork. Underneath the bluster, a polite request is hard for him to refuse—but his cooperation has a price: take [the unpleasant responsibility] off his hands.\n\n## Levers\n- **Polite request (weakness):** Ask nicely, and he finds it hard to say no. You earn a small tip or a more helpful answer.  \n  “Oh, you said *please*. Now I have to help. It’s in the pudding paperwork.”\n\n- **Agree to take [the unpleasant responsibility] (characterWants; obstacle: ForAPrice):** That’s his price. He warms to you and gives you his cooperation.  \n  “You’ll take it? Wonderful. I’ll put your name in the pudding as proof.”\n\n- **Offer to investigate the croissant theft (scene):** He gives you the job and a chance to earn his gratitude by finding [who keeps stealing the croissants].  \n  “Find the thief, and I’ll give you my full support—after I notarize it inside a pudding.”\n\n## Anti-levers\n- **Refuse his price (characterWants; obstacle: ForAPrice):** He won’t give you his goodwill for free; you’re left with his bare minimum help.  \n  “No responsibility, no extra help. That’s not a threat; it’s a very small contract.”\n\n- **Ask about the game he recently lost (willNotDiscuss):** He shuts the subject down and offers no more than he has to.  \n  “That game is closed. Please direct all questions to the pudding.”\n\n- **Make a demand without asking politely (weakness: finds it hard to refuse a polite request):** You miss the one easy way to soften him, and his temper holds.  \n  “That wasn’t a please. I can’t file that under ‘please.’ The pudding would reject it.”\n\n## Afterwards\nIf you take the responsibility, he leaves won over and tells everyone the pudding witnessed the agreement. If you don’t, he’s left as he was, still counting croissants. If you mock his paperwork or press him about the game, he makes you an enemy—and gives the pudding your description."
  },
  {
    "name": "Snort Wafflewick",
    "brief": [
      {
        "field": "setting",
        "value": "tiny bakery"
      },
      {
        "field": "playersWant",
        "value": null
      },
      {
        "field": "difficulty",
        "value": "Wall"
      },
      {
        "field": "occupation",
        "value": "baker"
      },
      {
        "field": "characterWants",
        "value": "to avoid someone they owe a favour to"
      },
      {
        "field": "obstacle",
        "value": "ForAPrice"
      },
      {
        "field": "attitude",
        "value": "furious"
      },
      {
        "field": "mannerism",
        "value": "speaks louder than necessary"
      },
      {
        "field": "weakness",
        "value": "feels obliged to repay small kindnesses"
      },
      {
        "field": "willNotDiscuss",
        "value": "their plans for later"
      },
      {
        "field": "wrongAbout",
        "value": "believes a harmless habit brings terrible luck"
      },
      {
        "field": "tone",
        "value": "Comic"
      },
      {
        "field": "race",
        "value": "goblin"
      },
      {
        "field": "nameLetter",
        "value": "S"
      },
      {
        "field": "nameSyllables",
        "value": "1"
      },
      {
        "field": "familyLetter",
        "value": "W"
      },
      {
        "field": "gender",
        "value": "Male"
      },
      {
        "field": "age",
        "value": "MiddleAged"
      },
      {
        "field": "look",
        "value": "unusual hair colour: copper red"
      }
    ],
    "sheet": "## Their name\nSnort Wafflewick\n\n## Read it to the table\n> You burst into a tiny bakery.\n> Snort is a middle-aged male goblin with copper-red hair.\n> He counts the croissants one by one, loudly accusing the empty spaces between them.\n> “Welcome! Mind the flour—it’s the only thing here that hasn’t been stolen, and I am watching it!”\n\n## Who they are\nSnort once tried to prove a croissant was circular by measuring it with a ruler, then got angry at the ruler.\n\n## Where they stand\nHe doesn’t know what you want from him yet. Someone keeps stealing his croissants, and he wants you to catch the thief: [who is stealing the croissants]. Alongside that, he wants your help avoiding [the person he owes a favour to]. That is the price for his goodwill; he won’t give it away just because you solve the croissant mystery.\n\n## How they seem, and what is underneath\nHe is furious, and conducts his croissant count at a volume suitable for addressing a much larger bakery. Underneath the pastry outrage, he wants help avoiding the person he owes a favour to. He feels obliged to repay even small kindnesses, but won’t discuss his plans for later.\n\n## Levers\n- **Catch the croissant thief** *(scene hook)*: He’ll give you the job, though it doesn’t by itself meet his price. “Excellent! Bring me the thief—or, failing that, bring me a croissant with a convincing alibi!”\n- **Help him avoid [the person he owes a favour to]** *(characterWants; ForAPrice)*: Meet the stated price and earn his goodwill. “You’ll help me dodge them? Wonderful. I shall be grateful at a volume that makes gratitude everyone’s problem.”\n- **Do him a small kindness** *(weakness)*: He’ll feel obliged to repay it with a small kindness of his own, but it won’t buy his full goodwill. “You’ve done me a tiny favour. I now owe you a tiny favour. Please don’t make it a large one; I’ve already measured it.”\n\n## Anti-levers\n- **Take or steal a croissant** *(scene; his fury)*: He’ll treat you as part of the pastry problem. “Put that back! I have already lost several croissants and most of my patience, and I’m counting both!”\n- **Insist he tell you his plans for later** *(willNotDiscuss)*: He’ll shut down that line of questioning. “My plans for later are private. Even I don’t know them yet, and I’m not telling you before I do.”\n- **Tell him to stop avoiding [the person he owes a favour to]** *(characterWants)*: He’ll dig in rather than pay the price you’re asking him to face. “Absolutely not. I have already avoided them with great commitment, and I won’t let that effort go to waste!”\n\n## Afterwards\n- **Won over:** If you help him avoid [the person he owes a favour to], he loudly declares you “trusted allies” and starts counting you as a kindness he must repay.\n- **Left as they were:** If you don’t meet his price, he goes back to counting croissants and accusing the gaps.\n- **Made an enemy:** If you worsen the pastry problem or push past his refusal, he tells every customer you’re “the croissant incident” and leaves out the parts where he did the shouting."
  },
  {
    "name": "Jib Rumblecrust",
    "brief": [
      {
        "field": "setting",
        "value": "tiny bakery"
      },
      {
        "field": "playersWant",
        "value": null
      },
      {
        "field": "difficulty",
        "value": "Easy"
      },
      {
        "field": "occupation",
        "value": "baker"
      },
      {
        "field": "characterWants",
        "value": "to find out whether a rumour is true"
      },
      {
        "field": "obstacle",
        "value": "Cant"
      },
      {
        "field": "attitude",
        "value": "furious"
      },
      {
        "field": "mannerism",
        "value": "finds something to fiddle with"
      },
      {
        "field": "weakness",
        "value": "finds it hard to refuse a polite request"
      },
      {
        "field": "willNotDiscuss",
        "value": "a game they recently lost"
      },
      {
        "field": "wrongAbout",
        "value": "believes expensive things are always better"
      },
      {
        "field": "tone",
        "value": "Comic"
      },
      {
        "field": "race",
        "value": "goblin"
      },
      {
        "field": "nameLetter",
        "value": "J"
      },
      {
        "field": "nameSyllables",
        "value": "1"
      },
      {
        "field": "familyLetter",
        "value": "R"
      },
      {
        "field": "gender",
        "value": "Male"
      },
      {
        "field": "age",
        "value": "MiddleAged"
      },
      {
        "field": "look",
        "value": "bald"
      }
    ],
    "sheet": "## Their name\nJib Rumblecrust\n\n## Read it to the table\n> You burst into a tiny bakery.\n> A middle-aged male goblin baker stands bald-headed behind the counter.\n> He furiously kneads dough with one hand and spins a wooden spoon with the other, as though interrogating it.\n> “Welcome! If you’re here to steal a croissant, please take a number—I’ve lost count of the suspects.”\n\n## Who they are\nJib once paid extra for a gold-plated dough scraper and still insists the loaves rise higher when it’s in the room.\n\n## Where they stand\nHe doesn’t know what you’ve come for, but he wants help stopping the croissant theft. Alongside that, he wants to find out whether [a rumour] is true. He can’t confirm it himself; his goodwill can get you his cooperation, not the answer.\n\n## How they seem, and what is underneath\nHe seems furious, punctuating every complaint by finding something to fiddle with. Underneath, he’s intensely curious about [a rumour] and absolutely certain that anything expensive must be better. He’ll warm to a polite request or a small, reasonable favour.\n\n## Levers\n- **A polite request** *(weakness; Easy)* gets his cooperation and is hard for him to refuse. “Oh! Manners. Yes, of course—inspect away. The croissants are excellent witnesses, if not especially good at staying in one place.”\n- **A small offer to help with the theft** *(scene hook; Easy)* wins his goodwill. “You’ll keep watch? Splendid. Try not to blink; apparently that’s when the pastries make their move.”\n- **Praise for expensive things** *(wrongAbout; Easy)* gets him talking and eager to help. “Finally, someone who understands! The pricier the tool, the more professionally it can point at a missing croissant.”\n\n## Anti-levers\n- **Call expensive equipment a waste of money** *(wrongAbout)* hardens him. “Waste? This spoon cost more than my first oven. It has excellent spoon credentials.”\n- **Demand answers rudely** *(weakness: he finds it hard to refuse a polite request)* puts him on his guard. “I was going to help, but now I’m busy being addressed like a bargain-bin rolling pin.”\n- **Ask about the game he recently lost** *(willNotDiscuss)* shuts down that line of conversation. “That game is over. Much like this conversation, if you bring it up again.”\n\n## Afterwards\n- **Won over:** He gives them his cooperation, then tells the story of their help as if they personally saved the entire bakery from pastry-related anarchy.\n- **Left as they were:** He goes back to interrogating the dough and keeps wondering whether [a rumour] is true.\n- **Made an enemy:** He remembers their offence with total conviction—and brings it up whenever anyone says the word “croissant.”"
  }
];
