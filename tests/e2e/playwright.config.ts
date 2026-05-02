import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  timeout: 30_000,
  fullyParallel: false,
  retries: 0,
  globalSetup: './global-setup.ts',
  outputDir: '/tmp/langoose-playwright-results',
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? 'http://app-web',
    headless: true,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure'
  }
});
