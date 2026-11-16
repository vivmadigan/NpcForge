import { expect, test, type Page } from '@playwright/test';
import { contrastFindings } from './contrast';

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

// The made-up seam answers after a second (ADR-018 decision 6), by a one-second timer in the page. A
// test that has to look at the wait holds that timer: the page keeps it and runs it only when the test
// says. The wait stays up until then, and ends the moment the seam answers. The page adds no timer of
// its own, so nothing else waits on it.
async function holdTheSeam(page: Page) {
  await page.addInitScript(() => {
    const held: (() => void)[] = [];
    const real = window.setTimeout.bind(window);
    window.setTimeout = ((handler: () => void, ms?: number, ...args: unknown[]) => {
      if (ms !== 1000) return real(handler, ms, ...args);
      held.push(() => handler());
      return 0;
    }) as typeof window.setTimeout;
    (window as unknown as { answerTheSeam: () => void }).answerTheSeam = () => held.splice(0).forEach((run) => run());
  });
}

const answerTheSeam = (page: Page) =>
  page.evaluate(() => (window as unknown as { answerTheSeam: () => void }).answerTheSeam());

async function sendTheScene(page: Page) {
  await holdTheSeam(page);
  await page.goto('/');
  await page.getByRole('textbox', { name: 'DESCRIBE YOUR SCENE' }).fill('An inn in a major city.');
  await page.getByRole('button', { name: 'SEND THE SCENE' }).click();
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
    // The cavern redraws when its box changes size, and a box of no size makes its canvas throw.
    const errors: string[] = [];
    page.on('pageerror', (error) => errors.push(error.message));
    await sendTheScene(page);
    await expect(status(page)).toBeVisible();
    expect(await fadeOf(page)).toEqual({ animation: 'none', opacity: '1' });
    await expect(page.locator('.under')).toBeHidden();
    expect(await contrastFindings(page)).toEqual([]);
    expect(errors).toEqual([]);
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

// The page adds no wait of its own: the screen is up for as long as the seam has not answered, and
// gone the moment it does. The seam is held, so the wait cannot end any other way. "The moment" is
// half a second, far under a wait a page might add.
const soon = 500;
test('the wait is shown after SEND THE SCENE and gone when the confirmation shows', async ({ page }) => {
  await sendTheScene(page);
  await expect(status(page)).toBeVisible();
  await page.waitForTimeout(1500);
  await expect(status(page)).toBeVisible();
  await expect(page.getByRole('region', { name: 'Who is being made' })).toHaveCount(0);

  await answerTheSeam(page);
  await expect(page.getByRole('region', { name: 'Who is being made' })).toBeVisible({ timeout: soon });
  await expect(status(page)).toHaveCount(0, { timeout: soon });
});

test('the wait is shown after ROLL IT and gone when the character shows', async ({ page }) => {
  await holdTheSeam(page);
  await page.goto('/?state=forge-confirm');
  await page.getByRole('button', { name: 'ROLL IT' }).click();
  await expect(status(page)).toBeVisible();
  await page.waitForTimeout(1500);
  await expect(status(page)).toBeVisible();
  await expect(page.getByRole('heading', { level: 1, name: 'AldoBun Sourdough' })).toHaveCount(0);

  await answerTheSeam(page);
  await expect(page.getByRole('heading', { level: 1, name: 'AldoBun Sourdough' })).toBeVisible({ timeout: soon });
  await expect(status(page)).toHaveCount(0, { timeout: soon });
});
