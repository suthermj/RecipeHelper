# Enhancements Backlog

A ranked list of enhancements, written so that a new session can pick one up and
work on it without extra context. `TODO.md` keeps the finer-grained checklist and
`MULTI_USER_ROADMAP.md` holds the multi-user design. This file is the menu of
features to choose from.

## How to use this file

1. **Pick one item** whose status is `Open` and whose **Depends on** items are done.
2. **Mark it `In progress`** with your branch name in a small first commit, so other
   sessions skip it.
3. Follow `CLAUDE.md`. In particular:
   - update `CHANGELOG.md` in the same PR
   - check any UI change with Playwright at iPhone size
   - prove a bug fix with a repro before fixing it
4. **Pushing a code change to a PR branch deploys it to production** (see CLAUDE.md,
   Deployment). Use a draft PR, or don't open one until you're ready.
5. **EF migrations:** create them from the latest `main`. Migrations on two branches
   at once conflict in `DatabaseContextModelSnapshot.cs`. After merging `main` in,
   regenerate your migration instead of hand-merging the snapshot.
6. When the work merges, set the status to `Done (#PR)`.

---

## 1. Sign-in and households

**Status:** Done on branch `claude/application-enhancements-88j0hs`, **not merged
yet**. The branch has a migration (`AddHouseholdsAndUsers`) that must be applied
before it deploys.

The branch contains:

- cookie sign-in: persistent, a 1-year sliding lifetime, and it survives deploys
- households with single-use, 7-day invite links (Settings → Household)
- sign-in required everywhere; signed-out visitors can only browse recipes read-only
  and open share links
- a rate limit on sign-in attempts
- 22 new tests

See the "Accounts & Households" section of CLAUDE.md on that branch.

**Any item that adds a controller or page should branch from `main` after this
merges**, or add `[AllowAnonymous]` only where signed-out access is intended.

Follow-ups (each can be its own PR once #1 is merged):

- **1a. Password reset.** There is none today. Suggested approach: another household
  member generates a one-time reset link from Settings, reusing the invite-token
  pattern (random token, only its hash stored, expiry, single use).
- **1b. Face ID sign-in (passkeys / WebAuthn).** Add it as an extra credential on an
  existing `AppUser`, so it belongs to the account and therefore the household.
  Password sign-in stays as the fallback.
- **1c. Phase 2: scope data by household.** Add `HouseholdId` to `Recipe`,
  `MealPlan` and `ShoppingList`, and fill it in for existing rows with the first
  household. Then filter every query by the signed-in user's household; the audit
  table in `MULTI_USER_ROADMAP.md` lists the query sites. Only after that, allow
  creating new households.
- **1d. Public/private recipes.** Add a visibility flag per recipe. Signed-out
  visitors can currently see *all* recipes read-only; after 1c they should see only
  public ones. **Depends on:** 1c.
- **1e. Smoke-test account.** Create a test member through an invite link and add
  the `SMOKE_TEST_EMAIL` / `SMOKE_TEST_PASSWORD` repository secrets. This is a
  manual step for the owner, not code.

## 2. Servings and scaling

**Status:** Open

- Store `Servings` on `Recipe`. Spoonacular imports already return it
  (`Models/Import/ImportRecipeViews.cs`), but it's dropped when the recipe is saved.
- Add a scale factor (½×, 1×, 2×) to each `MealPlanEntry`.
- In `SubmitDinnerSelections`, multiply each recipe's quantities by its entries'
  scale factors. Today it expands a recipe once per day it's planned; see Coding
  Conventions, "Ingredient aggregation".
- Show servings on the recipe view, with an optional on-screen scaler.
- **Files:** `Models/Recipe.cs`, `Models/Dinner/MealPlan.cs`,
  `Controllers/DinnerController.cs`, `Services/ImportService.cs`, the Recipe
  Create/Edit views, `Views/Dinner/Index.cshtml`.
- **Test:** extend `ReviewIngredientsMergeByUpcTests`-style tests so that a 2×
  entry doubles the quantities.

## 3. Real pantry list (issue #64)

**Status:** Done (#182). Follow-ups below are still open.

Shipped: a built-in default list in code (`Utility/PantryDefaults.cs`) plus a `PantryItem` table
for the user's own additions, edited on the Pantry page (Settings → Pantry), matched by linked Kroger UPC first, then whole-word match on the *end* of the
ingredient name (so "flour" matches "all-purpose flour" but not "flour tortillas", and
the seeded "black pepper" doesn't touch bell peppers). `PantryMatcher` does the matching
and `SubmitDinnerSelections` sets `IngredientVM.IsPantry`.

Follow-ups:

- **3a. "Add to pantry?" prompt.** Track how often each ingredient (by UPC, else
  normalized name) is unchecked on submit; after 3 skips in a row, show a dismissible
  card on the review page with Add / Not now. Needs a `PantrySkip` table (count,
  last skipped, dismissed). Reset the count when the row is left checked.
- **3b. Kroger category veto.** Store the product `categories` Kroger already returns
  on `KrogerProduct` (needs a column + backfill) and never treat Produce/Meat/Seafood
  products as pantry, whatever their name says. Only works for linked ingredients, and
  Kroger's category data is inconsistent, so use it as a veto only.
- **3c. Known false positives in the seed.** `water` matches "coconut water" and fresh
  `basil` / `thyme` match produce-section herbs; consider dried-only entries.
- **3d. Per household.** Once #1c lands, add `HouseholdId` to `PantryItem`.

## 4. Recipe share link (issue #91)

**Status:** Open. The issue has the full design, which mirrors the meal plan share
link (`ShareToken`, `ShareController`, `navigator.share`).

- New public route: `ShareController.Recipe`. `ShareController` is already
  `[AllowAnonymous]` on the #1 branch.
- Dedupe `GenerateShareToken()`. `MealPlanService` and `AccountService` both have
  one.

## 5. Meal history and suggestions

**Status:** Open

- Show "Last made N weeks ago" on recipe cards and in the meal plan picker, derived
  from past `MealPlanEntry` rows. This needs no schema change.
- Add "Copy last week" on `Dinner/Index`.
- Add "Suggest a week": pick recipes not made recently, spread across
  `DinnerCategory`. Server-side and deterministic enough to test.
- **Files:** `Services/MealPlanService.cs`, `Controllers/DinnerController.cs`,
  `Views/Dinner/Index.cshtml`, `Views/Recipe/Recipe.cshtml`.

## 6. Tags, favorites and picker search

**Status:** Open

- Recipes only have `DinnerCategory` today. Add free-form tags (quick, kid-friendly,
  freezer) and a favorite flag.
- Add filter chips on the recipe list and search in the meal plan picker overlay.
- After #1c, favorites should be per household.

## 7. Leftovers entry type

**Status:** Open

- Add a meal plan entry kind "Leftovers from <day>". It references another entry on
  the same plan and adds **no** ingredients to aggregation.
- Free-text placeholder entries already exist (`AddMealPlanEntryFreeText`
  migration); extend that model.

## 8. Weekly cost estimate

**Status:** Open

- Show an estimated total per recipe and per week on the meal plan, using the
  pricing the cart preview already computes, including sale prices and
  `KrogerBrandDiscount`.
- Cache prices, because Kroger rate-limits lookups (see #170 in `CHANGELOG.md`).
  Pairs with TODO.md "Stage 2" (caching pack data on `KrogerProduct`).

## 9. Cheaper alternative suggestions

**Status:** Open. **Depends on:** #8's price caching, to avoid extra Kroger calls.

- In the cart preview, when a mapped product isn't on sale and a close match is,
  show "Switch to X — save $Y".

## 10. Don't re-add what was already bought

**Status:** Open

- Record UPCs and quantities added to the Kroger cart per meal plan week.
- When the review is run again mid-week, pre-uncheck items already added.
- Related: TODO.md "Stage 1 — persist the ingredient review".

## 11. Photo import progress (issue #74)

**Status:** Open. The issue has the design and its tradeoffs: a job store,
`IServiceScopeFactory`, a status endpoint, reload recovery.

A cheaper alternative, also described there, is to shrink the image sent to the
vision model.

## 12. Offline cook mode

**Status:** Open

- Cook mode already holds a Screen Wake Lock (`ViewRecipe.cshtml`).
- Missing: pre-cache this week's planned recipe pages and images in `sw.js`, so
  recipes open without signal. Watch the existing stale-while-revalidate and
  `X-SW-No-Cache` rules. After #1, pages differ for signed-in and signed-out
  visitors, so don't serve a cached signed-in page after sign-out. The existing
  "mutation bypasses cache" logic covers this; keep it that way.

## 13. One-tap clipboard paste on Import (issue #94, part 2)

**Status:** Open. Needs a real-iPhone check (iOS paste-permission behavior), so
Chromium Playwright alone isn't evidence here. Part 1 of the issue (removing the
empty-state card) is already done.

## 14. Test coverage

**Status:** Open

- Meal plan add/remove/move endpoints, the import save path (`ImportService`,
  especially "only `SelectedUpc` is persisted"), and shopping list CRUD have no
  tests.
- Reuse the in-memory DB pattern from `ReviewIngredientsMergeByUpcTests`. The #1
  branch adds `AuthPipelineTests`, which boots the whole app in-process (useful for
  endpoint tests).
- Add `dotnet test` to `.github/workflows/build.yml`: CI builds but doesn't run the
  tests.

## 15. Alerting

**Status:** Open (Grafana configuration, mostly outside this repo)

- Alert on a 5xx rate spike, on Kroger 429s / lookup failures, and on the service
  being down. The telemetry already reaches Grafana Cloud (see Observability in
  CLAUDE.md).
- Document the alert rules in `deploy/README.md`.
