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

- [ ] Shopping list integration with Kroger
  - [x] Select the Kroger store you're shopping at via Kroger API
  - [x] Add location services to easily search for nearby Kroger stores
  - [x] Auto-detect store location change — if a different Kroger store is detected, prompt user to update store so aisle locations stay accurate
  - [x] Attach estimated prices to list items (query Kroger API); auto-apply 10% discount on Kroger brand products (employee discount)
  - [x] Sort list items by aisle number; produce, meat, and dairy should each be their own separate sections (not sorted with general aisles)
  - [x] Allow quantity updates on list items after the list is generated
  - [x] Allow checking off items as completed; completed items move to a "Completed" section at the bottom
- [x] When generating a list/adding to cart, allow auto-exclusion of bulk pantry items (e.g. spices) that don't need to be purchased every trip

## Infrastructure / DevOps

- [x] Integrate Grafana for logging and metrics
- [ ] Set up Prometheus with `prometheus-net.AspNetCore` to expose a `/metrics` endpoint
- [ ] Set up Loki + Promtail to ship logs from journald to Grafana Loki
- [ ] Add structured logging via Serilog for queryable logs in Loki/Grafana
- [ ] Create Grafana dashboards for request rate, error rate, response latency, and DB query times

## Bug Fixes / Improvements

- [x] Fix plural logic — "1 teaspoons of sugar" should display as "1 teaspoon of sugar"
- [x] Improve Kroger product search when linking ingredients — strip quantity/unit prefix so "1 tsp of sugar" searches as "sugar" instead of the full string

## Process / Best Practices

- [ ] No CI pipeline runs build/tests automatically on push or PR — builds are only validated locally before deploy
- [ ] No automated test suite beyond the Playwright UI checks documented in CLAUDE.md — no unit/integration tests for services (`MealPlanService`, `KrogerService`, `UnitConverter`, etc.)
- [ ] No dependency update automation (e.g. Dependabot) for NuGet/npm packages
- [ ] No documented backup/restore process for the Azure SQL database
- [ ] `CHANGELOG.md` was only just added (see #45) — historical entries prior to its creation weren't backfilled in detail
