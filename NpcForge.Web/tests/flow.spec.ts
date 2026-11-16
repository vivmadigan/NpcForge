import { readFileSync } from 'node:fs';
import { expect, test, type Page } from '@playwright/test';
import { bakeryCharacters } from '../src/madeUp/bakery';
import { contrastFindings } from './contrast';

// The run the made-up data is copied from. A Windows checkout may hold it with CRLF line ends.
const runFile = readFileSync(
  new URL('../../docs/runs/raw/2026-11-08-01-epic4-bakery-roll-again.md', import.meta.url),
  'utf8',
).replace(/\r\n/g, '\n');

test('each made-up sheet is the bakery run file, byte for byte', () => {
  expect(bakeryCharacters).toHaveLength(3);
  for (const { name, sheet } of bakeryCharacters) {
    // The whole sheet: from the line after the writer's last `[loop] ... out` trace line to the
    // line before the console's keep question.
    const at = runFile.indexOf(` out\n${sheet}\nKeep this character?`);
    expect(at, `${name}'s sheet is in the run file, whole`).toBeGreaterThan(-1);
    // The last name the run accepted before this sheet is this character's name.
    const named = runFile.slice(runFile.lastIndexOf('[app] name ', at));
    expect(named.startsWith(`[app] name ${name} (`), `the run named this sheet's character ${name}`).toBe(true);
  }
});

// Markdown's own marks are not letters or digits, so two lists are equal only when every word is on
// the page, in order, with none added (the same pattern as sheet.spec.ts).
const words = (text: string) => text.match(/[\p{L}\p{N}]+/gu) ?? [];

// The first two characters the bakery run wrote, by the names on its `[app] name` lines.
const [aldoBun, snort] = bakeryCharacters;

// The GM's own scene: the made-up interview answers any scene with the bakery run's brief.
const scene = 'An inn in a major city. We want the name of a fence.';

async function expectContrast(page: Page) {
  expect(await contrastFindings(page)).toEqual([]);
}

async function expectCharacter(page: Page, name: string, sheet: string) {
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(name);
  const shown = page.getByRole('region', { name: 'Character sheet' });
  expect(words(await shown.innerText())).toEqual(words(sheet));
}

const savedCount = (page: Page) => page.getByRole('navigation').getByRole('link', { name: /^SAVED/ });

async function describeAndConfirm(page: Page) {
  await page.goto('/');
  await expect(savedCount(page)).toHaveText('SAVED CHARACTERS 16');
  await expectContrast(page);

  await page.getByRole('textbox', { name: 'DESCRIBE YOUR SCENE' }).fill(scene);
  await page.getByRole('button', { name: 'SEND THE SCENE' }).click();
  await expect(page.getByRole('region', { name: 'Who is being made' })).toContainText('The NPC we are building is');
  await expect(page.getByRole('region', { name: 'Your scene' })).toContainText(scene);
  await expectContrast(page);

  await page.getByRole('button', { name: 'ROLL IT' }).click();
  await expect(page.getByRole('heading', { name: 'AT THE FORGE' })).toBeVisible();
  await expectCharacter(page, 'AldoBun Sourdough', aldoBun.sheet);
  await expect(page.getByRole('region', { name: 'Your scene' })).toContainText(scene);
  await expectContrast(page);
}

test('the whole flow: describe, confirm, read, roll again, save, open the list, open the save', async ({ page }) => {
  await describeAndConfirm(page);

  await page.getByRole('button', { name: 'ROLL AGAIN' }).click();
  await expectCharacter(page, 'Snort Wafflewick', snort.sheet);
  await expectContrast(page);

  await page.getByRole('button', { name: 'SAVE', exact: true }).click();
  await expect(page.getByText('Saved as 17.')).toBeVisible();
  await expect(savedCount(page)).toHaveText('SAVED CHARACTERS 17');
  await expectContrast(page);

  await page.getByRole('link', { name: 'OPEN THE SAVED LIST' }).click();
  await expect(page.getByRole('list').getByRole('link')).toHaveCount(17);
  await expectContrast(page);

  await page.getByRole('link', { name: /^17 / }).click();
  await expectCharacter(page, 'Snort Wafflewick', snort.sheet);
  await expect(savedCount(page)).toHaveText('SAVED CHARACTERS 17');
  await expectContrast(page);
});

test('a second run that ends with leave saves nothing', async ({ page }) => {
  await describeAndConfirm(page);

  await page.getByRole('button', { name: 'LEAVE' }).click();
  await expect(page.getByText('Not saved.')).toBeVisible();
  await expect(page.getByRole('textbox', { name: 'DESCRIBE YOUR SCENE' })).toHaveValue(scene);
  await expectContrast(page);

  await page.goto('/saved');
  await expect(page.getByRole('list').getByRole('link')).toHaveCount(16);
  await expect(savedCount(page)).toHaveText('SAVED CHARACTERS 16');
});

test('changing the scene on the confirmation rolls nothing and leaves the scene in the box', async ({ page }) => {
  await page.goto('/');
  await page.getByRole('textbox', { name: 'DESCRIBE YOUR SCENE' }).fill(scene);
  await page.getByRole('button', { name: 'SEND THE SCENE' }).click();
  await page.getByRole('button', { name: 'CHANGE THE SCENE' }).click();

  await expect(page.getByText('Not rolled.')).toBeVisible();
  await expect(page.getByRole('textbox', { name: 'DESCRIBE YOUR SCENE' })).toHaveValue(scene);
  await expect(page.getByRole('region', { name: 'Character sheet' })).toHaveCount(0);
  await expectContrast(page);
});

test('an answer to the question leads to the confirmation', async ({ page }) => {
  await page.goto('/?state=forge-question');
  await page.getByRole('textbox', { name: /^occupation/ }).fill('baker');
  await page.getByRole('button', { name: 'ANSWER' }).click();
  await expect(page.getByRole('region', { name: 'Who is being made' })).toContainText('The NPC we are building is');
  await expectContrast(page);
});

test('after no name, change the scene puts it back in the box', async ({ page }) => {
  await page.goto('/?state=forge-no-name');
  const shown = await page.getByRole('region', { name: 'Your scene' }).getByRole('paragraph').innerText();
  await page.getByRole('button', { name: 'CHANGE THE SCENE' }).click();
  await expect(page.getByRole('textbox', { name: 'DESCRIBE YOUR SCENE' })).toHaveValue(shown);
  await expect(page.getByRole('button', { name: 'SEND THE SCENE' })).toBeEnabled();
});

test('after no name, roll again writes a character', async ({ page }) => {
  await page.goto('/?state=forge-no-name');
  await page.getByRole('button', { name: 'ROLL AGAIN' }).click();
  await expectCharacter(page, 'AldoBun Sourdough', aldoBun.sheet);
  await expectContrast(page);
});

test('the example link puts a scene in the box, ready to send', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'SEND THE SCENE' })).toBeDisabled();
  await page.getByRole('button', { name: 'or try the bakery example' }).click();
  await expect(page.getByRole('textbox', { name: 'DESCRIBE YOUR SCENE' })).toHaveValue(/tiny bakery/);
  await expect(page.getByRole('button', { name: 'SEND THE SCENE' })).toBeEnabled();
  await expectContrast(page);
});
