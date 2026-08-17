# TODO

## UI / Navigation

- [x] Change "Dinners" nav label to "Meal Plan"
- [ ] Add a row of action buttons (icon + label, like the ReciMe reference screenshot) below the title on `Recipe/ViewRecipe`: **Meal Plan**, **Add to Cart**, **Add to List**, **Share** — no Pin/bookmark button
- [ ] Print recipe feature
- [ ] Share recipe feature (covered by the action-button row above)

## Meal Planning

- [x] Enable meal plan history — view previous weeks' meal plans
- [ ] Creating a meal plan should automatically generate the corresponding shopping list

## Shopping List

- [ ] **Meal-plan → Kroger cart pipeline redesign** — tracked in [#89](https://github.com/suthermj/RecipeHelper/issues/89), staged, one PR per stage:
  - [x] Stage 0 — fix confirmed conversion bugs (no schema changes); grew to include a same-UPC over-order fix and preferring Kroger's serving-size data over density guessing. PR [#88](https://github.com/suthermj/RecipeHelper/pull/88) (merged)
  - [ ] Stage 1 — persist the ingredient review (`ShoppingPlan`/`ShoppingPlanItem` keyed to `MealPlanId`); removes the session dependency that loses the review on every deploy
  - [ ] Stage 2 — narrowed after Stage 0: now mainly caching the serving-derived pack data on `KrogerProduct` (still re-fetched from Kroger on every preview) plus a manual-override UI for the no-serving-data cases, rather than an accuracy fix
  - [ ] Stage 3 — carry exact base-unit amounts end to end instead of rounded display values; root-causes #48
- [ ] Shopping list integration with Kroger
  - [x] Select the Kroger store you're shopping at via Kroger API
  - [x] Add location services to easily search for nearby Kroger stores
  - [x] Auto-detect store location change — if a different Kroger store is detected, prompt user to update store so aisle locations stay accurate
  - [x] Attach estimated prices to list items (query Kroger API); auto-apply 10% discount on Kroger brand products (employee discount)
  - [x] Sort list items by aisle number; produce, meat, and dairy should each be their own separate sections (not sorted with general aisles)
  - [x] Allow quantity updates on list items after the list is generated
  - [x] Allow checking off items as completed; completed items move to a "Completed" section at the bottom
- [x] When generating a list/adding to cart, allow auto-exclusion of bulk pantry items (e.g. spices) that don't need to be purchased every trip

## Architecture / Future

- [ ] Multi-user support (logins, private-by-default recipes with an opt-in Public flag, a "Discover" tab for browsing/copying other users' public recipes) — planning doc in [`MULTI_USER_ROADMAP.md`](MULTI_USER_ROADMAP.md), not started; architecture and data flows to be reviewed thoroughly before implementation begins

## Infrastructure / DevOps

- [x] Integrate Grafana for logging and metrics
- [ ] Set up Prometheus with `prometheus-net.AspNetCore` to expose a `/metrics` endpoint
- [ ] Set up Loki + Promtail to ship logs from journald to Grafana Loki
- [ ] Add structured logging via Serilog for queryable logs in Loki/Grafana
- [ ] Create Grafana dashboards for request rate, error rate, response latency, and DB query times
- [ ] Run `dotnet run -- --backfill-image-cache-headers` (see #55/#54) against production to backfill the `Cache-Control` header onto recipe images uploaded before that change — new uploads get it automatically at upload time, but existing blobs need this one-time manual run with prod Blob Storage credentials, which no automated session has

## Code Quality / Refactoring

- [ ] Split photo-related operations (DNG conversion, image compression/resizing, GPT-4o vision extraction) out of `IngredientsService` — an ingredient-focused service handling image processing and OpenAI vision calls doesn't seem like the right place for that logic. Worth separating the OpenAI/vision-extraction pieces from the raw image-processing pieces too, rather than moving everything into one new catch-all service

## Bug Fixes / Improvements

- [x] Fix plural logic — "1 teaspoons of sugar" should display as "1 teaspoon of sugar"
- [x] Improve Kroger product search when linking ingredients — strip quantity/unit prefix so "1 tsp of sugar" searches as "sugar" instead of the full string

## Process / Best Practices

- [ ] No CI pipeline runs build/tests automatically on push or PR — builds are only validated locally before deploy
- [ ] No automated test suite beyond the Playwright UI checks documented in CLAUDE.md — no unit/integration tests for services (`MealPlanService`, `UnitConverter`, etc.). `RecipeHelper.Tests` (xUnit) exists as of PR #88, currently only covering `KrogerService`'s pack-size/dimension-matching logic — worth expanding.
- [ ] No dependency update automation (e.g. Dependabot) for NuGet/npm packages
- [ ] No documented backup/restore process for the Azure SQL database
- [ ] `CHANGELOG.md` was only just added (see #45) — historical entries prior to its creation weren't backfilled in detail
