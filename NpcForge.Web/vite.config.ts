import { defineConfig } from 'vite';

// ADR-019 decision 8: a page calls /api on its own address, and the dev server forwards it to
// NpcForge.Api, so the page needs no API address and the API needs no CORS. The AppHost starts the
// pages with API_HTTP set to where it put the API, and Playwright sets it to its own API. With neither,
// as in `npm start`, it is the API's launch profile port. Nothing answers there unless the API runs, and
// each call then fails with the dev server's 502, which each page shows in its failure state.
export default defineConfig({
  server: { proxy: { '/api': process.env.API_HTTP || 'http://localhost:5180' } },
});
