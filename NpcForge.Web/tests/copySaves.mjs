// Makes the saves file the browser tests' API reads: the made-up saves, which are the owner's file as
// it stood at commit e3ac16e, copied to the path given. The API's webServer command runs it once, in
// Playwright's own process, before the API starts. It is not in playwright.config.ts because Playwright
// loads that file again in every worker, and a worker must never write the file the API holds
// (principle-separate-before-serializing-shared-state).
import { copyFileSync } from 'node:fs';

const [, , destination] = process.argv;
if (!destination) throw new Error('Usage: node copySaves.mjs <the file to write>');
copyFileSync(new URL('../src/madeUp/saves.json', import.meta.url), destination);
