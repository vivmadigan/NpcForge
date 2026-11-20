// Every spec takes `test` and `expect` from ./fixture, which fails a test whose page raised an
// uncaught error. A spec that imports them from '@playwright/test' has none of that, and nothing else
// would say so: its tests pass, and a handler deleted from the page goes unseen (epic 14, PR 7). Only
// types may come from the package, because a type has no effect at run time.
//
// `npm test` runs this before Playwright starts the API, so a bypass fails at once and not after a
// cold .NET build.
import { readdirSync, readFileSync } from 'node:fs';

const here = new URL('.', import.meta.url);
const specs = readdirSync(here).filter((name) => name.endsWith('.spec.ts'));
if (specs.length === 0) {
  console.error(`checkSpecImports: no *.spec.ts found in ${here.pathname}, so it checked nothing.`);
  process.exit(1);
}

// `import type { ... } from` and `import { type A, type B } from` the package, over any number of lines.
const typeOnlyImport =
  /^import\s+(?:type\s+[^;]*?|\{\s*(?:type\s+\w+\s*,?\s*)+\})\s+from\s+(['"])@playwright\/test\1;?/gm;
const comments = /\/\*[\s\S]*?\*\/|\/\/.*$/gm;

const bypassing = specs.filter((name) => {
  const code = readFileSync(new URL(name, here), 'utf8').replace(comments, '').replace(typeOnlyImport, '');
  return code.includes('@playwright/test');
});

for (const name of bypassing) {
  console.error(
    `tests/${name} uses '@playwright/test' for more than types. ` +
      "Take test and expect from './fixture', which fails a test whose page raised an uncaught error.",
  );
}
if (bypassing.length > 0) process.exit(1);
console.log(`checkSpecImports: ${specs.length} specs take test and expect from the fixture.`);
