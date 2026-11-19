import { expect, test, type Page, type Response } from '@playwright/test';
import { contrastFindings } from './contrast';
import { answers, confirmation, sceneBox, standInSheet, toConfirmation } from './forge';

// Epic 14's flow, on the real dice and the stand-in model: every step calls NpcForge.Api, and the save
// lands in the tests' copy of the saves file. Tests run in parallel on that one copy, so a test checks
// the save it made, by the number the page shows, and never a total (ADR-019, "Consequences").

// Markdown's own marks are not letters or digits, so two lists are equal only when every word is on
// the page, in order, with none added (the same pattern as sheet.spec.ts).
const words = (text: string) => text.match(/[\p{L}\p{N}]+/gu) ?? [];

// The GM's own scene. It names neither the place nor the role in a way the stand-in records, so the
// interview asks for both.
const scene = 'We want the name of a fence. Play it for laughs.';

type Written = { kind: 'written'; name: string; sheet: string };

async function expectContrast(page: Page) {
  expect(await contrastFindings(page)).toEqual([]);
}

async function expectCharacter(page: Page, name: string, sheet: string) {
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(name);
  const shown = page.getByRole('region', { name: 'Character sheet' });
  expect(words(await shown.innerText())).toEqual(words(sheet));
}

// The character the API sent for the roll the click starts. The page must show this one.
async function rolled(page: Page, click: () => Promise<void>): Promise<Written> {
  const answered = page.waitForResponse(
    (response: Response) => /\/api\/interviews\/[^/]+\/characters$/.test(new URL(response.url()).pathname),
  );
  await click();
  const body: Written = await (await answered).json();
  expect(body.kind).toBe('written');
  return body;
}

// The character page holds the three buttons twice, above the sheet and below it. The flow uses the
// ones at the top, which are the ones in view.
const keepBar = (page: Page) => page.getByRole('region', { name: 'Keep, roll again or leave', exact: true });

async function describeAndRoll(page: Page): Promise<Written> {
  await page.goto('/');
  await expectContrast(page);

  await sceneBox(page).fill(scene);
  await page.getByRole('button', { name: 'SEND THE SCENE' }).click();
  // Each question names its field and keeps the scene above it.
  for (const { field, text } of answers) {
    await expect(page.getByRole('region', { name: 'Your scene' })).toContainText(scene);
    const box = page.getByRole('textbox', { name: new RegExp(`^${field} `) });
    await expect(box).toBeVisible();
    await expectContrast(page);
    await box.fill(text);
    await page.getByRole('button', { name: 'ANSWER' }).click();
  }

  // The confirmation holds what the GM typed.
  for (const { text } of answers) await expect(confirmation(page)).toContainText(text, { ignoreCase: true });
  await expect(page.getByRole('region', { name: 'Your scene' })).toContainText(scene);
  await expectContrast(page);

  const character = await rolled(page, () => page.getByRole('button', { name: 'ROLL IT' }).click());
  // The stand-in wrote it, so no model was called.
  expect(character.sheet).toBe(standInSheet);
  await expectCharacter(page, character.name, character.sheet);
  await expect(page.getByRole('region', { name: 'Your scene' })).toContainText(scene);
  await expectContrast(page);
  return character;
}

test('the whole flow: describe, answer, confirm, read, roll again, save, open the list, open the save', async ({
  page,
}) => {
  const first = await describeAndRoll(page);

  const again = await rolled(page, () => keepBar(page).getByRole('button', { name: 'ROLL AGAIN' }).click());
  // New dice, so a new name.
  expect(again.name).not.toBe(first.name);
  await expectCharacter(page, again.name, again.sheet);
  await expectContrast(page);

  await keepBar(page).getByRole('button', { name: 'SAVE' }).click();
  const saved = await page.getByText(/^Saved as \d+\.$/).innerText();
  const number = Number(saved.match(/\d+/)![0]);
  // The 16 seeded saves are numbers 1 to 16, so a new one comes after them.
  expect(number).toBeGreaterThan(16);
  await expectContrast(page);

  await page.getByRole('link', { name: 'OPEN THE SAVED LIST' }).click();
  const row = page.getByRole('list').getByRole('link', { name: new RegExp(`^${number} `) });
  await expect(row).toContainText(again.name);
  await expect(row).toContainText('fence, an inn in a major city');
  await expectContrast(page);

  await row.click();
  await expect(page).toHaveURL(new RegExp(`/saved/${number}$`));
  await expectCharacter(page, again.name, again.sheet);
  await expectContrast(page);
});

test('a run that ends with leave saves nothing', async ({ page }) => {
  const saves: string[] = [];
  page.on('request', (request) => {
    if (request.url().includes('/api/drafts/')) saves.push(request.url());
  });
  await describeAndRoll(page);

  await keepBar(page).getByRole('button', { name: 'LEAVE' }).click();
  await expect(page.getByText('Not saved.')).toBeVisible();
  await expect(sceneBox(page)).toHaveValue(scene);
  await expectContrast(page);
  expect(saves).toEqual([]);
});

test('changing the scene on the confirmation rolls nothing and leaves the scene in the box', async ({ page }) => {
  const rolls: string[] = [];
  page.on('request', (request) => {
    if (request.url().endsWith('/characters')) rolls.push(request.url());
  });
  await page.goto('/');
  await toConfirmation(page, scene);
  await page.getByRole('button', { name: 'CHANGE THE SCENE' }).click();

  await expect(page.getByText('Not rolled.')).toBeVisible();
  await expect(sceneBox(page)).toHaveValue(scene);
  await expect(page.getByRole('region', { name: 'Character sheet' })).toHaveCount(0);
  await expectContrast(page);
  expect(rolls).toEqual([]);
});

test('after no name, change the scene puts it back in the box', async ({ page }) => {
  await page.goto('/?state=forge-no-name');
  const shown = await page.getByRole('region', { name: 'Your scene' }).getByRole('paragraph').innerText();
  await page.getByRole('button', { name: 'CHANGE THE SCENE' }).click();
  await expect(sceneBox(page)).toHaveValue(shown);
  await expect(page.getByRole('button', { name: 'SEND THE SCENE' })).toBeEnabled();
});

test('the example link puts a scene in the box, ready to send', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('button', { name: 'SEND THE SCENE' })).toBeDisabled();
  await page.getByRole('button', { name: 'or try the bakery example' }).click();
  await expect(sceneBox(page)).toHaveValue(/tiny bakery/);
  await expect(page.getByRole('button', { name: 'SEND THE SCENE' })).toBeEnabled();
  await expectContrast(page);
});
