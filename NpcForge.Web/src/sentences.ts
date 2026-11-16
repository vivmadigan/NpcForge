// ADR-018 decision 2: the confirmation's sentences, written from the entries the scene settled. A
// pure function with no React in it, so a test imports it as states.spec.ts imports the table of
// states. The page draws the parts and colours the values, and never searches a string for them.
import type { BriefField, ConfirmEntry } from './api';

// A part is the page's own words or a value from an entry. Free text is one value part, exactly as
// sent. A choice is one value part too, in its plain words from `plainWords`.
export type Part = { kind: 'text' | 'value'; text: string };
export type Sentence = Part[];

const text = (words: string): Part => ({ kind: 'text', text: words });

// Plain words for each choice the core's brief offers (NpcForge.Core/Brief/CharacterBrief.cs). A
// choice added to the core needs a line here, and `tsc` fails until it has one. The name letters
// need none: a letter is its own plain word.
type Choices = {
  difficulty: 'Easy' | 'SomeWork' | 'Wall';
  obstacle: 'Wont' | 'Cant' | 'ForAPrice';
  tone: 'Serious' | 'Comic' | 'Heartfelt';
  gender: 'Male' | 'Female';
  age: 'Teenager' | 'Adult' | 'MiddleAged' | 'Old';
  nameSyllables: '1' | '2' | '3';
};
const plainWords: { [Field in keyof Choices]: Record<Choices[Field], string> } = {
  difficulty: { Easy: 'easy', SomeWork: 'some work', Wall: 'a wall' },
  obstacle: { Wont: 'unwilling', Cant: 'unable', ForAPrice: 'willing for a price' },
  tone: { Serious: 'serious', Comic: 'comic', Heartfelt: 'heartfelt' },
  gender: { Male: 'male', Female: 'female' },
  age: { Teenager: 'teenage', Adult: 'adult', MiddleAged: 'middle-aged', Old: 'old' },
  nameSyllables: { '1': 'one syllable', '2': 'two syllables', '3': 'three syllables' },
};

// What a recorded value reads as on the page: a choice in its plain words, free text as sent. A
// choice this table does not know reads as sent, so a value is never dropped (epic 6's `api.ts`
// decides whether to refuse it).
export function plain(field: BriefField, value: string): string {
  const words: Record<string, string> | undefined = (plainWords as Record<string, Record<string, string>>)[field];
  return words !== undefined && Object.hasOwn(words, value) ? words[value] : value;
}

// The pronouns a sentence about the character uses. Only a gender the scene settled gets he or she;
// no gender entry gets they, so a sentence never guesses (ADR-018 decision 2, the scene with no gender).
type Who = { subject: string; object: string; possessive: string; be: string; s: string };
const they: Who = { subject: 'they', object: 'them', possessive: 'their', be: 'are', s: '' };
const pronouns: Record<Choices['gender'], Who> = {
  Male: { subject: 'he', object: 'him', possessive: 'his', be: 'is', s: 's' },
  Female: { subject: 'she', object: 'her', possessive: 'her', be: 'is', s: 's' },
};
function whoIs(entries: ConfirmEntry[]): Who {
  const gender = entries.find((entry) => entry.field === 'gender')?.value;
  return gender != null && Object.hasOwn(pronouns, gender) ? pronouns[gender as Choices['gender']] : they;
}

// One field's words, as a clause that a sentence joins to others. No clause puts an article or a
// preposition directly before its value, because the GM may have written "the Gilded Flagon" or a
// phrase that starts any way at all (ADR-018, Consequences). A value that is a whole phrase, whose
// shape the table cannot know, sits in quotation marks instead. `tsc` fails for a field with no line.
type Clause = (value: Part, who: Who) => Part[];
const words: Record<BriefField, Clause> = {
  setting: (v) => [text('the setting is '), v],
  playersWant: (v, who) => [text(`what the players want from ${who.object} is `), v],
  occupation: (v) => [text('whose occupation is '), v],
  difficulty: (v, who) => [text(`winning ${who.object} over is `), v],
  characterWants: (v, who) => [text(`what ${who.subject} want${who.s} is `), v],
  obstacle: (v, who) => [text(`${who.subject} ${who.be} `), v, text(' to give the players what they want')],
  attitude: (v, who) => [text(`${who.subject} ${who.be} currently `), v],
  mannerism: (v, who) => [text(`${who.possessive} mannerism is “`), v, text('”')],
  weakness: (v, who) => [text(`${who.possessive} weakness is “`), v, text('”')],
  willNotDiscuss: (v, who) => [text(`what ${who.subject} will not discuss is `), v],
  wrongAbout: (v, who) => [text(`what ${who.subject} ${who.be} wrong about is “`), v, text('”')],
  tone: (v) => [text('the tone you want is '), v],
  race: (v) => [text('whose race is '), v],
  nameLetter: (v, who) => [text(`${who.possessive} first name starts with `), v],
  nameSyllables: (v, who) => [text(`${who.possessive} first name has `), v],
  familyLetter: (v, who) => [text(`${who.possessive} family name starts with `), v],
  gender: (v) => [v],
  age: (v) => [v],
  look: (v, who) => [text(`what stands out about how ${who.subject} look${who.s} is `), v],
};

// ADR-010 decision 2: a want the GM did not give, said as the sentence the page writes for it.
const notGiven = (who: Who): Part[] => [
  text(
    `you did not say what the players want from ${who.object}, so ${who.subject} will be written around winning their goodwill`,
  ),
];

// Each sentence joins the fields that read together, so the page says a few sentences and not one for
// each field. The order is the page's own. The first sentence, about who they are, is `opening`,
// and `playersWant` comes last, as in the sketch.
const groups: BriefField[][] = [
  ['setting', 'attitude'],
  ['tone'],
  ['characterWants', 'difficulty', 'obstacle'],
  ['mannerism', 'weakness'],
  ['willNotDiscuss', 'wrongAbout'],
  ['look'],
  ['nameLetter', 'nameSyllables', 'familyLetter'],
  ['playersWant'],
];

function joined(clauses: Part[][], between: string, last: string): Part[] {
  return clauses.flatMap((clause, at) =>
    at === 0 ? clause : [text(at === clauses.length - 1 ? last : between), ...clause],
  );
}

function capital(sentence: Sentence): Sentence {
  const [first, ...rest] = sentence;
  return [{ ...first, text: first.text.charAt(0).toUpperCase() + first.text.slice(1) }, ...rest];
}

export function sentences(entries: ConfirmEntry[]): Sentence[] {
  const who = whoIs(entries);

  // An entry with a value is a clause. A want that was not given is the one null the page writes
  // about. A null for any other field has nothing to say, and is no clause.
  function clause(field: BriefField): Part[] | null {
    const entry = entries.find((candidate) => candidate.field === field);
    if (entry === undefined) return null;
    if (entry.value === null) return field === 'playersWant' ? notGiven(who) : null;
    return words[field]({ kind: 'value', text: plain(field, entry.value) }, who);
  }
  const present = (fields: BriefField[]) => fields.map(clause).filter((found) => found !== null);

  const result: Sentence[] = [];

  // Who they are: what they are like, then whose race and occupation, as the sketch's opening.
  const traits = present(['gender', 'age']);
  const people = present(['race', 'occupation']);
  if (traits.length + people.length > 0) {
    result.push(
      capital([
        text('the NPC we are building is '),
        ...joined(traits, ', ', ' and '),
        ...(traits.length > 0 && people.length > 0 ? [text(', ')] : []),
        ...(people.length > 0 ? [text('someone '), ...joined(people, ' and ', ' and ')] : []),
        text('.'),
      ]),
    );
  }

  for (const group of groups) {
    const found = present(group);
    if (found.length > 0) result.push(capital([...joined(found, ', ', ', and '), text('.')]));
  }
  return result;
}
