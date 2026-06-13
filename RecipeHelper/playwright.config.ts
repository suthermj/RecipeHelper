import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './tests/playwright',
  timeout: 15000,
  retries: 1,
  reporter: 'list',
  use: {
    baseURL: 'https://sutherlinsrecipes.duckdns.org',
    ignoreHTTPSErrors: false,
  },
  projects: [
    {
      name: 'iPhone 14',
      use: { ...devices['iPhone 14'] },
    },
  ],
});
