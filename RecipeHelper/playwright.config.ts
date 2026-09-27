import { defineConfig, devices } from '@playwright/test';
import { AUTH_STATE } from './tests/playwright/global-setup';

export default defineConfig({
  testDir: './tests/playwright',
  // Signs in once and shares the session with every test -- see global-setup.ts.
  globalSetup: './tests/playwright/global-setup.ts',
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
    storageState: AUTH_STATE,
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
