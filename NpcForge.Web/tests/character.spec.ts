import { expect, test, type Page } from '@playwright/test';

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
