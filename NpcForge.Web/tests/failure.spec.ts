import { expect, test, type Page } from '@playwright/test';
import { contrastFindings } from './contrast';
import { sceneBox, toConfirmation } from './forge';

// ADR-019 decision 4: every fault from the API ends in the page's one failure state, which shows the
// message. "No answer from the API" is made by aborting the page's own /api requests, so the request
// never reaches the API and the test needs no API of its own that is down (decision 10).
const noAnswer = (page: Page) => page.route('**/api/**', (route) => route.abort());

const savedLink = (page: Page) => page.getByRole('navigation').getByRole('link', { name: /^SAVED CHARACTERS/ });

const failure = (page: Page) => page.getByText(/^Failed: /);
const back = (page: Page) => page.getByRole('button', { name: 'BACK TO YOUR SCENE' });

test('with no answer from the API, sending the scene shows the failure state, with the scene kept', async ({
  page,
}) => {
  const scene = 'We want the name of a fence.';
  await page.goto('/');
  await noAnswer(page);
  await sceneBox(page).fill(scene);
  await page.getByRole('button', { name: 'SEND THE SCENE' }).click();

  await expect(failure(page)).toHaveText('Failed: The API did not answer.');
  await expect(page.getByRole('region', { name: 'Your scene' })).toContainText(scene);
  expect(await contrastFindings(page)).toEqual([]);
  await back(page).click();
  await expect(sceneBox(page)).toHaveValue(scene);
});

test('with no answer from the API, a save shows the failure state, with the scene kept', async ({ page }) => {
  const scene = 'We want the name of a fence.';
  await page.goto('/');
  await toConfirmation(page, scene);
  await page.getByRole('button', { name: 'ROLL IT' }).click();
  await expect(page.getByRole('region', { name: 'Character sheet' })).toBeVisible();

  await noAnswer(page);
  await page.getByRole('region', { name: 'Keep, roll again or leave', exact: true }).getByRole('button', { name: 'SAVE' }).click();
  await expect(failure(page)).toHaveText('Failed: The API did not answer.');
  await back(page).click();
  await expect(sceneBox(page)).toHaveValue(scene);
});

// A fault the API sends, and not one the test makes: the named state's interview is one the API
// never held, so ROLL AGAIN gets the API's 404 and its message (decision 4, its 404 row).
test('roll again on an interview the API does not hold shows the API\'s message, with the scene kept', async ({
  page,
}) => {
  await page.goto('/?state=forge-no-name');
  const scene = await page.getByRole('region', { name: 'Your scene' }).getByRole('paragraph').innerText();
  await page.getByRole('button', { name: 'ROLL AGAIN' }).click();

  await expect(failure(page)).toHaveText(
    'Failed: No interview with id bakery is held. A restart of the API forgets every interview.',
  );
  await back(page).click();
  await expect(sceneBox(page)).toHaveValue(scene);
});

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
