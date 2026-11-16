import { defineConfig, devices } from '@playwright/test';

// ADR-017 decision 2: Chromium only, two widths, and the dev server `npm start` would run
// (without the install and the browser tab). ADR-018 decision 8: every test and screenshot runs with
// less motion asked for, so the flame is held still and the contrast check and the screenshots are the
// same on every run. A test of the moving flame and the fade asks for normal motion itself.
export default defineConfig({
  testDir: 'tests',
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: true,
  },
  use: { baseURL: 'http://localhost:5173', reducedMotion: 'reduce' },
  projects: [
    { name: 'phone', use: { ...devices['Desktop Chrome'], viewport: { width: 390, height: 844 } } },
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } } },
  ],
});
