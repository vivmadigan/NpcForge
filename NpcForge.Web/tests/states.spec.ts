import { expect, test, type Page } from '@playwright/test';
import { bakeryScene } from '../src/madeUp/bakery';
import { namedStates } from '../src/madeUp/states';
import { contrastFindings } from './contrast';

// Every step after the first keeps the GM's scene above it.
async function sceneShown(page: Page, scene: string) {
  await expect(page.getByRole('region', { name: 'Your scene' })).toContainText(scene);
}

// What a user sees when the state has opened, written from the requirement and not read back from
// the table. A state with no entry here still has to show something other than "No state named".
const seen: Record<string, (page: Page) => Promise<void>> = {
  'saved-empty': async (page) => {
    await expect(page.getByRole('link', { name: 'SAVED CHARACTERS 0' })).toBeVisible();
    await expect(page.getByText('Nothing saved yet.')).toBeVisible();
    await expect(page.getByRole('link', { name: 'FORGE A CHARACTER' })).toHaveAttribute('href', '/');
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
    // A page this short is exactly one window tall: the cave takes what the header leaves.
    expect(await page.evaluate(() => document.documentElement.scrollHeight <= window.innerHeight)).toBe(true);
  },
  'forge-describe': async (page) => {
    await expect(page.getByRole('heading', { name: 'DESCRIBE YOUR SCENE' })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'DESCRIBE YOUR SCENE' })).toHaveValue('');
    await expect(page.getByRole('textbox', { name: 'DESCRIBE YOUR SCENE' })).toHaveAccessibleDescription(
      /^Where the players are/,
    );
    // The main button does nothing until the box has text.
    await expect(page.getByRole('button', { name: 'SEND THE SCENE' })).toBeDisabled();
    // The main button needs no scrolling to reach. The lava is below it.
    await expect(page.getByRole('button', { name: 'SEND THE SCENE' })).toBeInViewport({ ratio: 1 });
  },
  'forge-reading': async (page) => {
    await sceneShown(page, bakeryScene);
    await expect(page.getByText('The interviewer is reading your scene')).toBeVisible();
  },
  'forge-question': async (page) => {
    await sceneShown(page, 'someone is furious that the croissants keep vanishing');
    await expect(page.getByText('Your scene does not say this, and the dice cannot pick it:')).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'occupation Who the character is, such as innkeeper or farmer' })).toBeVisible();
  },
  'forge-confirm': async (page) => {
    await sceneShown(page, bakeryScene);
    await expect(page.getByText('Here is the character so far. Nothing is rolled until you say yes.')).toBeVisible();
    await expect(page.getByRole('listitem')).toHaveCount(19);
    await expect(page.getByRole('button', { name: 'ROLL IT' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'TURN IT DOWN' })).toBeVisible();
  },
  'forge-turned-down': async (page) => {
    await expect(page.getByText('Not rolled.')).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'DESCRIBE YOUR SCENE' })).toHaveValue(bakeryScene);
  },
  'forge-writing': async (page) => {
    await sceneShown(page, bakeryScene);
    await expect(page.getByRole('heading', { name: 'AT THE FORGE' })).toBeVisible();
    await expect(page.getByText('The dice settle who they are.')).toBeVisible();
    // Nothing to press while the character is written.
    await expect(page.getByRole('button')).toHaveCount(0);
  },
  'forge-written': async (page) => {
    await sceneShown(page, bakeryScene);
    await expect(page.getByRole('heading', { level: 1 })).toHaveText('AldoBun Sourdough');
    // Save, again and leave, each once on the page at either width.
    for (const action of ['SAVE', 'ROLL AGAIN', 'LEAVE']) {
      await expect(page.getByRole('button', { name: action, exact: true })).toHaveCount(1);
      await expect(page.getByRole('button', { name: action, exact: true })).toBeVisible();
    }
  },
  'forge-no-name': async (page) => {
    await expect(page.getByText('Not written: no name followed the dice (K, 2, V)')).toBeVisible();
    await expect(page.getByText('Nothing was saved.')).toBeVisible();
    await expect(page.getByRole('button', { name: 'ROLL AGAIN' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'CHANGE THE SCENE' })).toBeVisible();
  },
  'forge-cap': async (page) => {
    await expect(page.getByText(/^Stopped: Spending cap reached: .* Nothing was saved\.$/)).toBeVisible();
    // Nothing on the page asks the model for more.
    await expect(page.getByRole('button')).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'OPEN THE SAVED CHARACTERS' })).toBeVisible();
  },
  'forge-saved': async (page) => {
    await expect(page.getByText('Saved as 17.')).toBeVisible();
    await expect(page.getByRole('link', { name: 'OPEN SAVE 17' })).toHaveAttribute('href', '/saved/17');
    await expect(page.getByRole('link', { name: 'OPEN THE SAVED LIST' })).toHaveAttribute('href', '/saved');
  },
  'forge-left': async (page) => {
    await expect(page.getByText('Not saved.')).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'DESCRIBE YOUR SCENE' })).toHaveValue(bakeryScene);
  },
};

async function open(page: Page, path: string, name: string) {
  await page.goto(`${path}?state=${name}`);
  await expect(page.getByRole('link', { name: 'NPC FORGE', exact: true })).toBeVisible();
  await expect(page.getByText('No state named')).toHaveCount(0);
  await seen[name]?.(page);
  // A font that has not loaded changes the picture.
  await page.evaluate(() => document.fonts.ready);
}

for (const [name, { path }] of Object.entries(namedStates)) {
  test(`${name} opens and its text has AA contrast`, async ({ page }) => {
    await open(page, path, name);
    expect(await contrastFindings(page)).toEqual([]);
  });

  test(`${name} screenshot`, { tag: '@screenshots' }, async ({ page }, testInfo) => {
    await open(page, path, name);
    // The folder is relative to NpcForge.Web/, where Playwright runs.
    await page.screenshot({
      path: `../docs/design/epic-13/${name}-${testInfo.project.name}.png`,
      fullPage: true,
    });
  });
}

test('a name the table lacks shows "No state named" and not the default page', async ({ page }) => {
  await page.goto('/saved?state=no-such-state');
  await expect(page.getByText('No state named no-such-state')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'SAVED CHARACTERS' })).toHaveCount(0);
});

test('a real name opened on another path than its own shows the same', async ({ page }) => {
  // Taken from the table, so a renamed state cannot leave this passing on a name that is gone.
  const name = Object.keys(namedStates).find((key) => namedStates[key].path !== '/saved')!;
  await page.goto(`/saved?state=${name}`);
  await expect(page.getByText(`No state named ${name}`)).toBeVisible();
  await expect(page.getByRole('heading', { name: 'SAVED CHARACTERS' })).toHaveCount(0);
});

// `seen` is optional for a state, so a renamed state would lose its entry without a word.
test('every state the spec describes is in the table', () => {
  expect(Object.keys(seen).filter((name) => !Object.hasOwn(namedStates, name))).toEqual([]);
});
