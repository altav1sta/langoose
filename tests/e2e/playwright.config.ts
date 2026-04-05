import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  timeout: 30_000,
  fullyParallel: false,
  retries: 0,
  outputDir: '/tmp/langoose-playwright-results',
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? 'http://web-e2e',
    headless: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  }
});
