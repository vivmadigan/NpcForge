import { expect, test, type Page } from '@playwright/test';
import { toConfirmation } from './forge';

// ADR-018 decision 9: no page shows the rolled brief, on the forge page or on an opened save.
async function expectNoBrief(page: Page) {
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  await expect(page.getByRole('complementary')).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'BRIEF', exact: true })).toHaveCount(0);
  await expect(page.getByRole('term')).toHaveCount(0);
}

test('no brief list beside the character, on the forge page', async ({ page }) => {
  await page.goto('/?state=forge-written');
  await expectNoBrief(page);
});

test('no brief list beside the character, on an opened save', async ({ page }) => {
  await page.goto('/saved/13?state=saved-open');
  await expectNoBrief(page);
});

// The epic's test: the GM answers without scrolling. Save, roll again and leave in the top panel are
// fully inside the window the moment the page opens, at both widths.
const topButtons = (page: Page) =>
  page.getByRole('region', { name: 'Keep, roll again or leave', exact: true }).getByRole('button');

async function expectTopButtonsInView(page: Page) {
  for (const name of ['SAVE', 'ROLL AGAIN', 'LEAVE']) {
    await expect(topButtons(page).filter({ hasText: new RegExp(`^${name}$`) })).toBeInViewport({ ratio: 1 });
  }
  expect(await page.evaluate(() => window.scrollY)).toBe(0);
}

test('save, roll again and leave are in view when the character page opens, with no scrolling', async ({ page }) => {
  await page.goto('/?state=forge-written');
  await expectTopButtonsInView(page);
});

test('save, roll again and leave are in view when the character page opens after ROLL IT', async ({ page }) => {
  await page.goto('/');
  await toConfirmation(page, 'We want the name of a fence.');
  // A GM who read to the foot of the confirmation, so the character page opens from a scrolled
  // window, and the page has to scroll back up itself (lesson 022: the test reaches the case).
  await page.evaluate(() => window.scrollTo(0, document.documentElement.scrollHeight));
  expect(await page.evaluate(() => window.scrollY)).toBeGreaterThan(0);
  await page.getByRole('button', { name: 'ROLL IT' }).click();
  await expect(page.getByRole('region', { name: 'Character sheet' })).toBeVisible();
  await expectTopButtonsInView(page);
});

test('the character page puts the sheet between two sets of the same three buttons', async ({ page }) => {
  await page.goto('/?state=forge-written');
  const below = page.getByRole('region', { name: 'Keep, roll again or leave, below the sheet', exact: true });
  const sheet = page.getByRole('region', { name: 'Character sheet' });
  const [top, middle, foot] = await Promise.all([
    topButtons(page).first().boundingBox(),
    sheet.boundingBox(),
    below.getByRole('button').first().boundingBox(),
  ]);
  expect(top!.y).toBeLessThan(middle!.y);
  expect(middle!.y).toBeLessThan(foot!.y);
});

test('an opened save is BACK TO SAVED and the parchment, with no keep buttons', async ({ page }) => {
  await page.goto('/saved/13');
  await expect(page.getByRole('link', { name: 'BACK TO SAVED' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Character sheet' })).toBeVisible();
  await expect(page.getByRole('button')).toHaveCount(0);
});
