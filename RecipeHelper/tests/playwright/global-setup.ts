import { request, type FullConfig } from '@playwright/test';

export const AUTH_STATE = 'tests/playwright/.auth/state.json';

// The app requires signing in for everything except recipe browsing and share links,
// so the suite signs in once with a dedicated household member account and reuses the
// session cookie (storageState) for every test. Create that account with an invite
// link from Settings -> Household, then store its credentials as the
// SMOKE_TEST_EMAIL / SMOKE_TEST_PASSWORD repository secrets (smoke-test.yml passes
// them through).
export default async function globalSetup(config: FullConfig) {
  const email = process.env.SMOKE_TEST_EMAIL;
  const password = process.env.SMOKE_TEST_PASSWORD;
  if (!email || !password) {
    throw new Error(
      'SMOKE_TEST_EMAIL and SMOKE_TEST_PASSWORD must be set: the app requires sign-in. ' +
      'See tests/playwright/global-setup.ts.');
  }

  const baseURL = config.projects[0].use.baseURL;
  const ctx = await request.newContext({ baseURL });

  const loginPage = await ctx.get('/Account/Login');
  const html = await loginPage.text();
  const token = html.match(/name="__RequestVerificationToken" type="hidden" value="([^"]+)"/)?.[1];
  if (!token) throw new Error(`No antiforgery token on /Account/Login (HTTP ${loginPage.status()})`);

  const response = await ctx.post('/Account/Login', {
    form: { Email: email, Password: password, __RequestVerificationToken: token },
    maxRedirects: 0,
  });
  // Success redirects away from the login page; a wrong password re-renders it (200).
  if (response.status() !== 302) {
    throw new Error(`Smoke test sign-in failed (HTTP ${response.status()}) -- check the SMOKE_TEST_* secrets.`);
  }

  await ctx.storageState({ path: AUTH_STATE });
  await ctx.dispose();
}
