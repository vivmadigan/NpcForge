import { expect, test } from './fixture';
import { namedStates } from '../src/madeUp/states';
import { plain } from '../src/sentences';
import { contrastFindings } from './contrast';

// What the GM reads on the confirmation, for both made-up confirmations. The values come from the
// state's own entries, and a choice is read through its plain words (ADR-018 decision 2).
const gendered = /\b(he|him|his|she|her|hers)\b/i;
const joinedNames = ['playersWant', 'characterWants', 'willNotDiscuss', 'wrongAbout', 'nameLetter', 'nameSyllables', 'familyLetter'];

for (const name of ['forge-confirm', 'forge-confirm-market']) {
  const { state } = namedStates[name];
  if (state.kind !== 'confirm') throw new Error(`${name} is not a confirmation`);

  test(`${name} holds the scene and every recorded value, ignoring case`, async ({ page }) => {
    await page.goto(`/?state=${name}`);
    await expect(page.getByRole('region', { name: 'Your scene' })).toContainText(state.scene);
    const sentences = page.getByRole('region', { name: 'Who is being made' });
    for (const { field, value } of state.entries) {
      if (value === null) continue;
      await expect(sentences, `the sentences hold ${field}`).toContainText(plain(field, value), { ignoreCase: true });
    }
  });

  test(`${name} shows no joined field name and no label`, async ({ page }) => {
    await page.goto(`/?state=${name}`);
    await expect(page.getByRole('region', { name: 'Who is being made' })).toBeVisible();
    const everything = (await page.locator('body').innerText()).toLowerCase();
    for (const field of joinedNames) expect(everything, `no ${field} on the page`).not.toContain(field.toLowerCase());
    expect(everything).not.toMatch(/\[(my words|inferred|not given|rolled)\]|typed when asked/);
    await expect(page.getByRole('listitem')).toHaveCount(0);
  });
}

test('the bakery scene did not say what the players want, and the page says so', async ({ page }) => {
  await page.goto('/?state=forge-confirm');
  const sentences = page.getByRole('region', { name: 'Who is being made' });
  await expect(sentences).toContainText(/did not say what the players want/i);
  await expect(sentences).toContainText(/winning their goodwill/i);
});

test('the market scene gave a want and no gender, so the page shows the want and no gendered pronoun', async ({ page }) => {
  await page.goto('/?state=forge-confirm-market');
  const sentences = page.getByRole('region', { name: 'Who is being made' });
  await expect(sentences).toContainText('the fastest road to the capital');
  await expect(sentences).not.toContainText(/did not say what the players want/i);
  expect(await sentences.innerText()).not.toMatch(gendered);
});

test('change the scene on the confirmation puts the scene back in the box and rolls nothing', async ({ page }) => {
  await page.goto('/?state=forge-confirm-market');
  await expect(page.getByRole('button', { name: 'ROLL IT' })).toBeVisible();
  await page.getByRole('button', { name: 'CHANGE THE SCENE' }).click();
  await expect(page.getByText('Not rolled.')).toBeVisible();
  await expect(page.getByRole('textbox', { name: 'DESCRIBE YOUR SCENE' })).toHaveValue(
    (namedStates['forge-confirm-market'].state as { scene: string }).scene,
  );
  expect(await contrastFindings(page)).toEqual([]);
});
