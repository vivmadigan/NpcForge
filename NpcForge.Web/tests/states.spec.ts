import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { namedStates } from '../src/madeUp/states';

type AxeResults = Awaited<ReturnType<AxeBuilder['analyze']>>;

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
  // A font that has not loaded changes both the contrast axe measures and the picture.
  await page.evaluate(() => document.fonts.ready);
}

// ADR-017 decision 7: a result axe cannot judge fails like a violation, and each line names the
// element and the colours so a failure can be acted on.
function findings(results: AxeResults): string[] {
  const lines = (rules: AxeResults['violations'], verdict: string) =>
    rules.flatMap((rule) =>
      rule.nodes.map((node) => {
        const why = [...node.any, ...node.all, ...node.none].map((check) => check.message).join(' ');
        return `${verdict}: ${node.target.join(' ')} ${node.html.slice(0, 100)}\n    ${why}`;
      }),
    );
  return [...lines(results.violations, 'violation'), ...lines(results.incomplete, 'could not judge')];
}

for (const [name, { path }] of Object.entries(namedStates)) {
  test(`${name} opens and its text has AA contrast`, async ({ page }) => {
    await open(page, path, name);
    const results = await new AxeBuilder({ page }).withRules(['color-contrast']).analyze();
    expect(findings(results)).toEqual([]);
  });

  test(`${name} screenshot`, { tag: '@screenshots' }, async ({ page }, testInfo) => {
    await open(page, path, name);
    // The folder is relative to NpcForge.Web/, where Playwright runs.
    await page.screenshot({
      path: `../docs/design/epic-12/${name}-${testInfo.project.name}.png`,
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
