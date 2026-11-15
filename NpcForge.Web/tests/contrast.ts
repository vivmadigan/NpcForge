import AxeBuilder from '@axe-core/playwright';
import type { Page } from '@playwright/test';

type AxeResults = Awaited<ReturnType<AxeBuilder['analyze']>>;

// ADR-017 decision 7: axe's color-contrast rule on the page as it is now. A result axe cannot judge
// fails like a violation, and each line names the element and the colours so a failure can be acted
// on. The named states and every step of the flow use this one check.
export async function contrastFindings(page: Page): Promise<string[]> {
  // A font that has not loaded changes the contrast axe measures.
  await page.evaluate(() => document.fonts.ready);
  const results = await new AxeBuilder({ page }).withRules(['color-contrast']).analyze();
  const lines = (rules: AxeResults['violations'], verdict: string) =>
    rules.flatMap((rule) =>
      rule.nodes.map((node) => {
        const why = [...node.any, ...node.all, ...node.none].map((check) => check.message).join(' ');
        return `${verdict}: ${node.target.join(' ')} ${node.html.slice(0, 100)}\n    ${why}`;
      }),
    );
  return [...lines(results.violations, 'violation'), ...lines(results.incomplete, 'could not judge')];
}
