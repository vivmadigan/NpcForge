import { expect, test } from '@playwright/test';
import { savedList } from '../src/madeUp/saves';

// Epic 13: a header on every page links to the saved characters. Both Playwright projects run each
// test, so the link is found and used at phone and at desktop width. Other tests save to the same
// copy of the saves file while these run, so the list is checked for the 16 seeded saves, each by its
// number and name, and never by its length (ADR-019, "Consequences").
const pages = [
  { name: 'the forge page', path: '/' },
  { name: 'an opened save', path: '/saved/3' },
];

for (const { name, path } of pages) {
  test(`from ${name} the header opens the saved list`, async ({ page }) => {
    await page.goto(path);
    await page.getByRole('navigation').getByRole('link', { name: /^SAVED CHARACTERS/ }).click();

    await expect(page).toHaveURL(/\/saved$/);
    await expect(page.getByRole('heading', { name: 'SAVED CHARACTERS' })).toBeVisible();
    const list = page.getByRole('list');
    for (const { number, name } of savedList) {
      await expect(list.getByRole('link', { name: new RegExp(`^${number} `) })).toContainText(name ?? '(no name)');
    }
    // The header says which page the visitor is on.
    await expect(page.getByRole('navigation').getByRole('link', { name: /^SAVED CHARACTERS/ })).toHaveAttribute(
      'aria-current',
      'page',
    );
  });
}

test('the header names the forge page when the visitor is on it, and its title links home', async ({ page }) => {
  await page.goto('/saved');
  await expect(page.getByRole('navigation').getByRole('link', { name: 'FORGE', exact: true })).not.toHaveAttribute(
    'aria-current',
    'page',
  );
  await page.getByRole('link', { name: 'NPC FORGE', exact: true }).click();
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole('navigation').getByRole('link', { name: 'FORGE', exact: true })).toHaveAttribute(
    'aria-current',
    'page',
  );
});

test('the first step opens on the NPC FORGE title', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1, name: 'NPC FORGE' })).toBeVisible();
  // The title is over the cavern, so it is a picture and not text (epic 13).
  await expect(page.getByRole('img', { name: 'NPC FORGE' })).toBeVisible();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('');
});
