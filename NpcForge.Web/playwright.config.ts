import { randomUUID } from 'node:crypto';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { defineConfig, devices } from '@playwright/test';

// ADR-017 decision 2, as ADR-019 decision 10 amends it: Chromium only, two widths, and the dev server
// `npm start` would run (without the install and the browser tab), now beside an API of its own.
// ADR-018 decision 8: every test and screenshot runs with less motion asked for, so the flame is held
// still and the contrast check and the screenshots are the same on every run. A test of the moving flame
// and the fade asks for normal motion itself.

// Ports of their own, never 5173 or the API's 5180, which are where `npm start` and the API's launch
// profile are, so a server the owner has running is never the one under test (lesson 018).
const apiPort = 5381;
const pagesPort = 5382;

// A new file for each run, so one run never reads what another left. This line also runs in every
// worker, where the value is never used: nothing here creates the file (tests/copySaves.mjs does).
const savesFile = join(tmpdir(), `npcforge-playwright-saves-${randomUUID()}.json`);

export default defineConfig({
  testDir: 'tests',
  webServer: [
    {
      name: 'API',
      // The copy comes first and the API reads it. `dotnet run` builds the API, so a cold start waits for
      // a restore and a compile, and the timeout allows for that. The API's store is the file, so a test
      // that saves changes this copy and never the owner's characters.json. NPCFORGE_OFFLINE is here so
      // that a model client built later by mistake fails, and no route calls one yet.
      command: `node tests/copySaves.mjs "${savesFile}" && dotnet run --project ../NpcForge.Api --no-launch-profile`,
      url: `http://localhost:${apiPort}/api/saves`,
      env: { ASPNETCORE_URLS: `http://localhost:${apiPort}`, NPCFORGE_CHARACTERS: savesFile, NPCFORGE_OFFLINE: '1' },
      reuseExistingServer: false,
      timeout: 240_000,
    },
    {
      name: 'pages',
      // The last --port wins, as it does when Aspire starts the same script (ADR-019, "Context").
      command: `npm run dev -- --port ${pagesPort}`,
      url: `http://localhost:${pagesPort}`,
      env: { API_HTTP: `http://localhost:${apiPort}` },
      reuseExistingServer: false,
    },
  ],
  use: { baseURL: `http://localhost:${pagesPort}`, reducedMotion: 'reduce' },
  projects: [
    { name: 'phone', use: { ...devices['Desktop Chrome'], viewport: { width: 390, height: 844 } } },
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } } },
  ],
});
