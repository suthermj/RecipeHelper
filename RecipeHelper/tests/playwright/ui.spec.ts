import { test, expect } from '@playwright/test';

// ─── Tab bar ─────────────────────────────────────────────────────────────────

test.describe('Tab bar', () => {
  test('renders on Recipes, Meal Plan, Lists pages', async ({ page }) => {
    // Check a tab label child (backdrop-filter on the nav container can confuse WebKit visibility checks)
    for (const url of ['/Recipe', '/Dinner/Index', '/ShoppingList/Index']) {
      await page.goto(url);
      await expect(page.locator('nav.ios-tab .ios-tab-item').first()).toBeVisible();
    }
  });

  test('has four tab links (Recipes, Meal Plan, Lists, Settings)', async ({ page }) => {
    await page.goto('/Recipe');
    const nav = page.locator('nav.ios-tab');
    await expect(nav.getByText('Recipes')).toBeVisible();
    await expect(nav.getByText('Meal Plan')).toBeVisible();
    await expect(nav.getByText('Lists')).toBeVisible();
    await expect(nav.getByText('Settings')).toBeVisible();
    // Products must NOT appear as a tab
    await expect(nav.getByText('Products')).not.toBeVisible();
  });

  test('active tab highlights red on Recipes page', async ({ page }) => {
    await page.goto('/Recipe');
    const recipesTab = page.locator('nav.ios-tab a').filter({ hasText: 'Recipes' });
    // Color is set via inline style attribute
    const style = await recipesTab.getAttribute('style');
    expect(style).toContain('FF3B30');
  });

  test('active tab highlights red on Meal Plan page', async ({ page }) => {
    await page.goto('/Dinner/Index');
    // Target by href since the label text is split across SVG + span
    const tab = page.locator('nav.ios-tab a[href*="Dinner"]');
    await expect(tab).toBeAttached();
    const style = await tab.getAttribute('style');
    expect(style).toContain('FF3B30');
  });

  test('Settings tab highlights on Products page', async ({ page }) => {
    await page.goto('/Product/Products');
    const tab = page.locator('nav.ios-tab a').filter({ hasText: 'Settings' });
    const style = await tab.getAttribute('style');
    expect(style).toContain('FF3B30');
  });
});

// ─── Center + FAB & action sheet ─────────────────────────────────────────────

test.describe('Add recipe FAB', () => {
  test('+ button is visible in tab bar', async ({ page }) => {
    await page.goto('/Recipe');
    await expect(page.locator('#tabPlusBtn')).toBeVisible();
  });

  test('+ button has a touch target of at least 44px', async ({ page }) => {
    await page.goto('/Recipe');
    const box = await page.locator('#tabPlusBtn').boundingBox();
    expect(box).not.toBeNull();
    expect(box!.width).toBeGreaterThanOrEqual(44);
    expect(box!.height).toBeGreaterThanOrEqual(44);
  });

  test('tapping + opens the action sheet', async ({ page }) => {
    await page.goto('/Recipe');
    await page.locator('#tabPlusBtn').tap();
    await expect(page.locator('#addSheet')).toHaveClass(/is-open/);
    await expect(page.getByText('Import recipe')).toBeVisible();
    await expect(page.getByText('Create from scratch')).toBeVisible();
  });

  test('Cancel button closes the action sheet', async ({ page }) => {
    await page.goto('/Recipe');
    await page.locator('#tabPlusBtn').tap();
    await expect(page.locator('#addSheet')).toHaveClass(/is-open/);
    await page.locator('#addSheetCancel').tap();
    await expect(page.locator('#addSheet')).not.toHaveClass(/is-open/);
  });

  test('tapping backdrop closes the action sheet', async ({ page }) => {
    await page.goto('/Recipe');
    await page.locator('#tabPlusBtn').tap();
    await expect(page.locator('#addSheetBackdrop')).toHaveClass(/is-open/);
    await page.locator('#addSheetBackdrop').tap();
    await expect(page.locator('#addSheet')).not.toHaveClass(/is-open/);
  });

  test('Import recipe link points to import page', async ({ page }) => {
    await page.goto('/Recipe');
    await page.locator('#tabPlusBtn').tap();
    const href = await page.locator('#addSheet a').filter({ hasText: 'Import recipe' }).getAttribute('href');
    expect(href).toContain('Import');
  });

  test('Create from scratch link points to create page', async ({ page }) => {
    await page.goto('/Recipe');
    await page.locator('#tabPlusBtn').tap();
    const href = await page.locator('#addSheet a').filter({ hasText: 'Create from scratch' }).getAttribute('href');
    expect(href).toContain('Recipe');
  });
});

// ─── Nav bar conditional rendering ───────────────────────────────────────────

test.describe('Nav bar', () => {
  test('NOT rendered on Recipes page (no wasted space)', async ({ page }) => {
    await page.goto('/Recipe');
    await expect(page.locator('header.ios-nav')).not.toBeVisible();
  });

  test('NOT rendered on Meal Plan page', async ({ page }) => {
    await page.goto('/Dinner/Index');
    await expect(page.locator('header.ios-nav')).not.toBeVisible();
  });

  test('rendered on Settings page with correct title', async ({ page }) => {
    await page.goto('/Settings/Index');
    await expect(page.locator('header.ios-nav')).toBeVisible();
    await expect(page.locator('header.ios-nav')).toContainText('Settings');
  });

  test('rendered on Import page with back button labelled Recipes', async ({ page }) => {
    await page.goto('/Import/ImportRecipe');
    await expect(page.locator('header.ios-nav')).toBeVisible();
    await expect(page.locator('header.ios-nav a').filter({ hasText: 'Recipes' })).toBeVisible();
  });
});

// ─── Recipes page ────────────────────────────────────────────────────────────

test.describe('Recipes page', () => {
  test('shows large title', async ({ page }) => {
    await page.goto('/Recipe');
    await expect(page.locator('h1.ios-large-title')).toBeVisible();
    await expect(page.locator('h1.ios-large-title')).toContainText('Recipes');
  });

  test('search bar is present', async ({ page }) => {
    await page.goto('/Recipe');
    const searchOrEmpty = page.locator('.ios-search-input, h2:has-text("No recipes")');
    await expect(searchOrEmpty.first()).toBeVisible();
  });

  test('no inline Import link in page body (moved to + sheet)', async ({ page }) => {
    await page.goto('/Recipe');
    // The old "Import" link that lived next to the large title should be gone
    const inlineImport = page.locator('.ios-large-title').locator('..').getByText('Import');
    await expect(inlineImport).not.toBeVisible();
  });
});

// ─── Touch targets ────────────────────────────────────────────────────────────

test.describe('Touch targets ≥ 44px', () => {
  test('tab bar items are tall enough', async ({ page }) => {
    await page.goto('/Recipe');
    const tabs = page.locator('nav.ios-tab .ios-tab-item');
    const count = await tabs.count();
    for (let i = 0; i < count; i++) {
      const box = await tabs.nth(i).boundingBox();
      if (box) {
        expect(box.height).toBeGreaterThanOrEqual(44);
      }
    }
  });

  test('nav bar back button is tall enough when present', async ({ page }) => {
    await page.goto('/Import/ImportRecipe');
    const backBtn = page.locator('header.ios-nav a').filter({ hasText: 'Recipes' });
    const box = await backBtn.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.height).toBeGreaterThanOrEqual(44);
  });
});

// ─── Safe area / layout ───────────────────────────────────────────────────────

test.describe('Layout', () => {
  test('page content has bottom padding to clear the fixed tab bar', async ({ page }) => {
    await page.goto('/Recipe');
    const pb = await page.locator('main').evaluate(el => parseFloat(getComputedStyle(el).paddingBottom));
    expect(pb).toBeGreaterThanOrEqual(58);
  });

  test('tab bar is fixed at the bottom', async ({ page }) => {
    await page.goto('/Recipe');
    const position = await page.locator('nav.ios-tab').evaluate(el => getComputedStyle(el).position);
    expect(position).toBe('fixed');
  });
});
