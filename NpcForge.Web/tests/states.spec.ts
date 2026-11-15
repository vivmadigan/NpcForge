import { expect, test, type Page } from '@playwright/test';
import { namedStates } from '../src/madeUp/states';

// What a user sees when the state has opened, written from the requirement and not read back from
// the table. A state with no entry here still has to show something other than "No state named".
const seen: Record<string, (page: Page) => Promise<void>> = {
  'saved-empty': async (page) => {
    await expect(page.getByRole('link', { name: 'SAVED: 0' })).toBeVisible();
    await expect(page.getByText('Nothing saved yet.')).toBeVisible();
  },
  'saved-long': async (page) => {
    await expect(page.getByRole('list').getByRole('link')).toHaveCount(16);
  },
  'saved-open': async (page) => {
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Caladri Hollowbottle');
  },
  'saved-open-unnamed': async (page) => {
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('(no name)');
    await expect(page.getByRole('region', { name: 'Character sheet' }).locator('blockquote').first()).toBeVisible();
  },
  'saved-missing': async (page) => {
    await expect(page.getByText('No save numbered 99.')).toBeVisible();
  },
};

async function open(page: Page, path: string, name: string) {
  await page.goto(`${path}?state=${name}`);
  await expect(page.getByRole('link', { name: 'NPCFORGE', exact: true })).toBeVisible();
  await expect(page.getByText('No state named')).toHaveCount(0);
  await seen[name]?.(page);
}

for (const [name, { path }] of Object.entries(namedStates)) {
  test(`${name} opens`, async ({ page }) => {
    await open(page, path, name);
  });
}

test('a name the table lacks shows "No state named" and not the default page', async ({ page }) => {
  await page.goto('/saved?state=no-such-state');
  await expect(page.getByText('No state named no-such-state')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'SAVED CHARACTERS' })).toHaveCount(0);
});

test('a real name opened on another path than its own shows the same', async ({ page }) => {
  await page.goto('/saved?state=saved-open');
  await expect(page.getByText('No state named saved-open')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'SAVED CHARACTERS' })).toHaveCount(0);
});
