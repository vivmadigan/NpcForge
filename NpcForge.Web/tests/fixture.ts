import { expect, test as base } from '@playwright/test';

// Every spec takes `test` and `expect` from here and never from '@playwright/test' (tests/checkSpecImports.mjs
// holds that). A page that throws, or leaves a promise rejected with no handler, does not fail a test
// on its own: the page carries on and the checks that follow can all pass. In epic 14 a test passed with
// the header's fault handler deleted while the page was reporting the rejection, and only a reviewer
// read it. So the page's `pageerror` events are collected for every test that uses `page`, and the
// test fails when it ends with any. A check kept in one spec alone leaves every other spec open to the
// same miss.
//
// A test that makes the page throw on purpose says so with `test.use({ allowPageErrors: true })`, at
// the top of its file or inside the `describe` that holds it. None does today.
export const test = base.extend<{ allowPageErrors: boolean }>({
  allowPageErrors: [false, { option: true }],

  // `page` is overridden, not wrapped in an auto fixture, so a test that never opens a page (the
  // pure-function specs) never pays for one.
  page: async ({ page, allowPageErrors }, use) => {
    const errors: Error[] = [];
    page.on('pageerror', (error) => errors.push(error));

    await use(page);

    // The browser reports an error a moment after it happens, so a test that ends on the action that
    // caused it would end first. One task turn lets the report arrive. A page the test closed, or one
    // that crashed, has nothing more to report.
    if (!page.isClosed()) {
      await page.evaluate(() => new Promise((resolve) => setTimeout(resolve))).catch(() => {});
    }

    if (errors.length > 0 && !allowPageErrors) {
      const lines = errors.map((error) => `  ${error.name}: ${error.message}`).join('\n');
      throw new Error(
        `The page raised ${errors.length} uncaught error${errors.length === 1 ? '' : 's'}:\n${lines}\n` +
          'A test that makes the page throw on purpose opts out with test.use({ allowPageErrors: true }).',
      );
    }
  },
});

export { expect };
