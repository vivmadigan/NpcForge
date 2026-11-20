import type { Page } from '@playwright/test';
import { expect, test } from './fixture';
import { contrastFindings } from './contrast';
import { sceneBox, toConfirmation } from './forge';

// ADR-018 decision 8: sending a scene and rolling fade to black, a flame burns while the seam
// answers, and a device that asks for less motion gets black at once and a still flame. The config
// asks for less motion for every test, so the flame is still unless a test says otherwise.
const status = (page: Page) => page.getByRole('status');

// One number for the flame's pixels, so two moments can be compared.
async function flamePixels(page: Page): Promise<number> {
  return page.evaluate(() => {
    const canvas = document.querySelector<HTMLCanvasElement>('canvas.flame')!;
    const { data } = canvas.getContext('2d')!.getImageData(0, 0, canvas.width, canvas.height);
    let sum = 0;
    for (let i = 0; i < data.length; i++) sum = (Math.imul(sum, 31) + data[i]) | 0;
    return sum;
  });
}

const fadeOf = (page: Page) =>
  status(page).evaluate((wait) => ({
    animation: getComputedStyle(wait).animationName,
    opacity: getComputedStyle(wait).opacity,
  }));

// The stand-in answers in a few milliseconds, so a test that has to look at the wait holds the API's
// answer: the request goes to the API at once, and its answer reaches the page only when the test
// says (lesson 021, with the seam now the API). The wait stays up until then, and ends the moment the
// answer arrives. The API's own time is spent before the hold ends, so it is not inside `soon` below.
// The page adds no timer of its own, so nothing else waits on it. Every timer is the real one, so axe
// runs.
async function holdTheAnswer(page: Page, route: string): Promise<() => void> {
  let answer!: () => void;
  const answered = new Promise<void>((resolve) => (answer = resolve));
  await page.route(route, async (held) => {
    const response = await held.fetch();
    await answered;
    await held.fulfill({ response });
  });
  return answer;
}

async function sendTheScene(page: Page): Promise<() => void> {
  await page.goto('/');
  const answer = await holdTheAnswer(page, '**/api/interviews');
  await sceneBox(page).fill('We want the name of a fence.');
  await page.getByRole('button', { name: 'SEND THE SCENE' }).click();
  return answer;
}

test.describe('with less motion asked for', () => {
  for (const name of ['forge-reading', 'forge-writing']) {
    test(`${name} holds still, with no fade`, async ({ page }) => {
      await page.goto(`/?state=${name}`);
      await expect(status(page)).toBeVisible();
      // Black, over the whole window, at once.
      const covers = await status(page).evaluate((wait) => {
        const box = wait.getBoundingClientRect();
        return {
          black: getComputedStyle(wait).backgroundColor,
          full: box.width === innerWidth && box.height === innerHeight,
        };
      });
      expect(covers).toEqual({ black: 'rgb(0, 0, 0)', full: true });
      expect(await fadeOf(page)).toEqual({ animation: 'none', opacity: '1' });
      // The flame is drawn, and the same at two moments.
      const first = await flamePixels(page);
      expect(first).not.toBe(0);
      await page.waitForTimeout(700);
      expect(await flamePixels(page)).toBe(first);
    });
  }

  test('in the real flow the black is there at once and the old step is not drawn', async ({ page }) => {
    // The cavern redraws when its box changes size, and a box of no size makes its canvas throw. The
    // shared fixture fails the test on that error.
    await sendTheScene(page);
    await expect(status(page)).toBeVisible();
    expect(await fadeOf(page)).toEqual({ animation: 'none', opacity: '1' });
    await expect(page.locator('.under')).toBeHidden();
    expect(await contrastFindings(page)).toEqual([]);
  });

  test('on a phone held sideways the wait fits the window', async ({ page }) => {
    await page.setViewportSize({ width: 844, height: 390 });
    await page.goto('/?state=forge-writing');
    await expect(status(page).getByText('This takes a little while.')).toBeInViewport({ ratio: 1 });
  });
});

test.describe('with normal motion', () => {
  test.use({ reducedMotion: 'no-preference' });

  test('the flame burns: its pixels differ between two moments', async ({ page }) => {
    await page.goto('/?state=forge-writing');
    await expect(status(page)).toBeVisible();
    const first = await flamePixels(page);
    await expect.poll(() => flamePixels(page), { timeout: 3000 }).not.toBe(first);
  });

  test('the black fades in over the step the GM left, which is then hidden', async ({ page }) => {
    await sendTheScene(page);
    await expect(status(page)).toBeVisible();
    expect((await fadeOf(page)).animation).toBe('wait-in');
    // Under the fade, the scene form is still drawn, and no one can reach it.
    const under = page.locator('.under');
    await expect(under).toHaveAttribute('inert', '');
    await expect(under).toHaveAttribute('aria-hidden', 'true');
    // The black is fully in, so the step under it is hidden, and the wait's own text is judged. The
    // wait is still up: the seam has not answered.
    await expect(under.locator('textarea')).toBeHidden();
    await expect(status(page)).toBeVisible();
    expect(await contrastFindings(page)).toEqual([]);
  });
});

// The page adds no wait of its own: the screen is up for as long as the API has not answered, and
// gone the moment it does. The answer is held, so the wait cannot end any other way. "The moment" is
// half a second, far under a wait a page might add.
const soon = 500;
test('the wait is shown after SEND THE SCENE and gone when the first question shows', async ({ page }) => {
  const answer = await sendTheScene(page);
  const question = page.getByRole('heading', { name: 'ONE QUESTION' });
  await expect(status(page)).toBeVisible();
  await page.waitForTimeout(1500);
  await expect(status(page)).toBeVisible();
  await expect(question).toHaveCount(0);

  answer();
  await expect(question).toBeVisible({ timeout: soon });
  await expect(status(page)).toHaveCount(0, { timeout: soon });
});

test('the wait is shown after ROLL IT and gone when the character shows', async ({ page }) => {
  await page.goto('/');
  await toConfirmation(page, 'We want the name of a fence.');
  const answer = await holdTheAnswer(page, '**/api/interviews/*/characters');
  const sheet = page.getByRole('region', { name: 'Character sheet' });
  await page.getByRole('button', { name: 'ROLL IT' }).click();
  await expect(status(page)).toBeVisible();
  await page.waitForTimeout(1500);
  await expect(status(page)).toBeVisible();
  await expect(sheet).toHaveCount(0);

  answer();
  await expect(sheet).toBeVisible({ timeout: soon });
  await expect(status(page)).toHaveCount(0, { timeout: soon });
});
