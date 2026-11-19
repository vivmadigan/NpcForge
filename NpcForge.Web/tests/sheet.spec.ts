import { expect, test } from '@playwright/test';
import rawSaves from '../src/madeUp/saves.json' with { type: 'json' };

// The saved text and the saved name are read from the file itself, so a page that drops or adds a
// word, or joins a name wrongly, fails here (ADR-017 decision 4).
const saves: Record<string, { text: string; name?: { syllables: string[]; family: string } }> = rawSaves;

// Markdown's own marks are not letters or digits, so two lists are equal only when every word is on
// the page, in order, with none added.
const words = (text: string) => text.match(/[\p{L}\p{N}]+/gu) ?? [];

// The API's saves are a copy of the same file the made-up saves are, so the page would show the same
// words if it never asked the API. Each test waits for the API's answer, so a page that reads
// made-up data instead fails here (ADR-019 decision 8).
const answered = (path: string) => (response: { url(): string; ok(): boolean }) =>
  new URL(response.url()).pathname === path && response.ok();

// Other tests save to the same copy while these run, so no test here holds a total (ADR-019,
// "Consequences"). The header and the list each ask for the saves, and a save can land between the
// two answers, so the list holds every row of the answer waited for, and may hold more.
test('the saved list shows every save the API sent, and the 16 seeded ones', async ({ page }) => {
  const listed = page.waitForResponse(answered('/api/saves'));
  await page.goto('/saved');
  const rows: { number: number }[] = await (await listed).json();
  expect(rows.length).toBeGreaterThanOrEqual(Object.keys(saves).length);
  const list = page.getByRole('list');
  for (const { number } of rows) {
    await expect(list.getByRole('link', { name: new RegExp(`^${number} `) })).toBeVisible();
  }
});

for (const [number, save] of Object.entries(saves)) {
  test(`save ${number} opens with its name and every word of its text, in order`, async ({ page }) => {
    await page.goto('/saved');
    const opened = page.waitForResponse(answered(`/api/saves/${number}`));
    await page.getByRole('link', { name: new RegExp(`^${number} `) }).click();
    await opened;

    const sheet = page.getByRole('region', { name: 'Character sheet' });
    await expect(sheet).toBeVisible();
    expect(words(await sheet.innerText())).toEqual(words(save.text));

    // The words alone would pass on the raw text with its marks showing. Every saved sheet has
    // six headings, so a sheet drawn as Markdown shows six.
    await expect(sheet.getByRole('heading')).toHaveCount(6);

    const name = save.name ? `${save.name.syllables.join('')} ${save.name.family}` : '(no name)';
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(name);

    // The header counts the saves on a page that does not hold the list: the 16 seeded saves, and
    // any another test has made since.
    const count = page.getByRole('navigation').getByRole('link', { name: /^SAVED CHARACTERS/ });
    await expect(count).toHaveText(/^SAVED CHARACTERS \d+$/);
    expect(Number((await count.innerText()).match(/\d+/)![0])).toBeGreaterThanOrEqual(16);
  });
}
