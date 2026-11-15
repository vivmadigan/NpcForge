import { defineConfig, devices } from '@playwright/test';

// ADR-017 decision 2: Chromium only, two widths, and the dev server `npm start` would run
// (without the install and the browser tab).
export default defineConfig({
  testDir: 'tests',
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:5173',
    reuseExistingServer: true,
  },
  use: { baseURL: 'http://localhost:5173' },
  projects: [
    { name: 'phone', use: { ...devices['Desktop Chrome'], viewport: { width: 390, height: 844 } } },
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } } },
  ],
});
