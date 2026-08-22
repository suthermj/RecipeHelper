import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/playwright',
  timeout: 15000,
  retries: 1,
  // 'list' alone never writes playwright-report/ -- smoke-test.yml's "Upload
  // Playwright report" step has been running on every CI failure and finding
  // nothing to upload. Adding the html reporter (open: 'never' so it doesn't
  // try to launch a browser in CI) plus trace/screenshot/video capture below
  // means the next flake actually leaves evidence instead of being diagnosed
  // blind.
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: 'https://sutherlinsrecipes.duckdns.org',
    ignoreHTTPSErrors: false,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'iPhone 14',
      use: { ...devices['iPhone 14'] },
    },
  ],
});
