import { expect, test, type Page } from '@playwright/test';
import { contrastFindings } from './contrast';

// ADR-019 decision 4: every fault from the API ends in the page's one failure state, which shows the
// message. "No answer from the API" is made by aborting the page's own /api requests, so the request
// never reaches the API and the test needs no API of its own that is down (decision 10).
const noAnswer = (page: Page) => page.route('**/api/**', (route) => route.abort());

const savedLink = (page: Page) => page.getByRole('navigation').getByRole('link', { name: /^SAVED CHARACTERS/ });

test('with no answer from the API, the saved list shows the failure state', async ({ page }) => {
  await noAnswer(page);
  await page.goto('/saved');
  await expect(page.getByText('The saves could not be read: The API did not answer.')).toBeVisible();
  await expect(page.getByRole('link', { name: 'TRY AGAIN' })).toHaveAttribute('href', '/saved');
  // The header has no count to show, so it shows none.
  await expect(savedLink(page)).toHaveText('SAVED CHARACTERS');
  expect(await contrastFindings(page)).toEqual([]);
});

test('with no answer from the API, an opened save shows the failure state', async ({ page }) => {
  await noAnswer(page);
  await page.goto('/saved/3');
  await expect(page.getByText('The saves could not be read: The API did not answer.')).toBeVisible();
  await expect(page.getByRole('link', { name: 'TRY AGAIN' })).toHaveAttribute('href', '/saved/3');
});
