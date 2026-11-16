import { expect, test } from '@playwright/test';
import type { BriefField, ConfirmEntry } from '../src/api';
import { plain, sentences, type Part } from '../src/sentences';

// The page's sentences are a pure function, so these tests import it as states.spec.ts imports the
// table of states. A Record keyed by every field, so a field added to BriefField fails tsc here too.
const everyField: Record<BriefField, string | null> = {
  setting: 'the Gilded Flagon',
  playersWant: 'a way into the vault',
  occupation: 'innkeeper',
  difficulty: 'SomeWork',
  characterWants: 'to be left alone',
  obstacle: 'ForAPrice',
  attitude: 'world-weary',
  mannerism: 'drums their fingers on the bar',
  weakness: 'vain',
  willNotDiscuss: 'their family',
  wrongAbout: 'believes the rat is a spy',
  tone: 'Heartfelt',
  race: 'half-orc',
  nameLetter: 'M',
  nameSyllables: '2',
  familyLetter: 'V',
  gender: 'Female',
  age: 'MiddleAged',
  look: 'a scar across one cheek',
};
const fields = Object.keys(everyField) as BriefField[];
const entriesFor = (list: BriefField[], value = everyField): ConfirmEntry[] =>
  list.map((field) => ({ field, value: value[field] }));

const textOf = (list: Part[][], kind: Part['kind']) =>
  list.flatMap((sentence) => sentence.filter((part) => part.kind === kind).map((part) => part.text));
const whole = (list: Part[][]) => list.map((sentence) => sentence.map((part) => part.text).join('')).join(' ');
const gendered = /\b(he|him|his|she|her|hers)\b/i;

test('a want that was not given says so, and the character is written around winning their goodwill', () => {
  const written = whole(sentences([{ field: 'playersWant', value: null }]));
  expect(written).toMatch(/did not say what the players want/i);
  expect(written).toMatch(/winning their goodwill/i);
});

test('a want that was given is a sentence that holds those words, and does not say it was missing', () => {
  const given = sentences([{ field: 'playersWant', value: 'the fastest road to the capital' }]);
  expect(whole(given)).toContain('the fastest road to the capital');
  expect(textOf(given, 'value')).toEqual(['the fastest road to the capital']);
  expect(whole(given)).not.toMatch(/did not say/i);
});

test('with no want entry at all, nothing is said about the players', () => {
  const written = whole(sentences(entriesFor(['setting', 'occupation', 'tone'])));
  expect(written).not.toMatch(/players/i);
});

test('with no gender entry, no sentence holds a gendered pronoun, whichever other fields are there', () => {
  const noGender = fields.filter((field) => field !== 'gender');
  for (const wantGiven of [null, 'the fastest road to the capital']) {
    const list = entriesFor(noGender, { ...everyField, playersWant: wantGiven });
    expect(textOf(sentences(list), 'text').join('')).not.toMatch(gendered);
  }
});

test('a gender the scene settled gets its own pronouns, and nothing else does', () => {
  const he = whole(sentences(entriesFor(['gender', 'attitude'], { ...everyField, gender: 'Male' })));
  expect(he).toMatch(/\bhe is currently\b/i);
  const she = whole(sentences(entriesFor(['gender', 'attitude'], { ...everyField, gender: 'Female' })));
  expect(she).toMatch(/\bshe is currently\b/i);
});

test('a value typed when asked is an entry like any other, so no part carries a mark', () => {
  // ADR-018 decision 5: the entry has no provenance, so the sentence cannot show one.
  const written = sentences(entriesFor(['occupation', 'setting']));
  for (const part of written.flat()) expect(Object.keys(part).sort()).toEqual(['kind', 'text']);
  expect(whole(written)).not.toMatch(/typed|my words|inferred|\[/i);
});

test('free text is one value part, exactly as sent, with no article or preposition directly before it', () => {
  const before = /\b(a|an|the|of|in|at|on|to|from|with|for|about|by|into)\s*$/i;
  const free: BriefField[] = [
    'setting', 'playersWant', 'occupation', 'characterWants', 'attitude', 'mannerism', 'weakness',
    'willNotDiscuss', 'wrongAbout', 'race', 'look',
  ];
  for (const field of free) {
    // Each alone, so each clause's own opening words are the ones checked.
    const [sentence] = sentences(entriesFor([field]));
    const at = sentence.findIndex((part) => part.kind === 'value');
    expect(sentence[at].text, `${field} appears as sent`).toBe(everyField[field]);
    expect(sentence.filter((part) => part.kind === 'value'), `${field} is one value part`).toHaveLength(1);
    expect(sentence[at - 1].text, `${field}'s lead-in`).not.toMatch(before);
  }
});

test('all 19 fields give no joined field name in any text part, and every value as a value part', () => {
  const written = sentences(entriesFor(fields));
  const joinedNames = fields.filter((field) => /[a-z][A-Z]/.test(field));
  expect(joinedNames.length).toBeGreaterThan(0);
  const textParts = textOf(written, 'text').join(' ').toLowerCase();
  for (const field of joinedNames) {
    expect(textParts, `no ${field} in the page's own words`).not.toContain(field.toLowerCase());
  }
  expect(textOf(written, 'value').sort()).toEqual(fields.map((field) => plain(field, everyField[field]!)).sort());
  // A few sentences, not one for each field.
  expect(written.length).toBeLessThan(fields.length / 2);
});

test('any one field alone is one sentence that starts with a capital and ends with a full stop', () => {
  for (const field of fields) {
    const [sentence, ...rest] = sentences(entriesFor([field]));
    const text = sentence.map((part) => part.text).join('');
    expect(rest, `${field} is one sentence`).toEqual([]);
    expect(text, field).toMatch(/^[A-Z].*\.$/);
  }
});

test('a choice reads in its plain words', () => {
  expect(plain('tone', 'Comic')).toBe('comic');
  expect(plain('difficulty', 'SomeWork')).toBe('some work');
  expect(plain('setting', 'Comic')).toBe('Comic');
});
