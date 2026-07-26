# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project is deployed continuously (no version tags), so entries are grouped
by date rather than by release version.

## [Unreleased]

### Added

- Manual "Deploy to Production" GitHub Actions workflow (`workflow_dispatch`, runnable from the GitHub mobile app), reaching the VM over Tailscale via OIDC federated identity so no long-lived secret is stored in GitHub.
- Service worker `CACHE_VERSION` is now auto-stamped to the deployed commit SHA by both `deploy/deploy.sh` and the GitHub Actions deploy workflow, so every deploy evicts old PWA caches automatically instead of relying on someone remembering to hand-bump the version.
- "Update available — tap to refresh" banner (`site.js`) when a new service worker finishes installing. `sw.js` no longer calls `skipWaiting()` unconditionally on install, so an already-open tab isn't silently swapped to a new version mid-session — the user chooses when to reload.

### Changed

- Photo import's recipe extraction (`IngredientsService.ExtractRecipeFromNormalizedPhotosAsync`) now uses `gpt-5.4` instead of `gpt-4o` for the vision call, aiming for more accurate ingredient/quantity extraction from recipe photos.

### Fixed

- Recipe picker sheet on Meal Plan losing its header, search bar, and backdrop on reopen: `max-height: 88svh` was only ever set as an inline style, so `closePicker()`'s `pickerCard.style.maxHeight = ''` reset wiped it permanently instead of falling back to a base value. Moved the 88svh cap into the `#recipePickerCard` CSS rule (same pattern already used for the keyboard-open 72svh override) so clearing the inline override correctly restores it. (#34)
- The above picker fix wasn't reaching installed PWAs because the Meal Plan page is served via `staleWhileRevalidate` and the service worker cache version hadn't changed — fixed by the auto-stamping change above.
- Deploy workflow originally took a `ref` text input that duplicated the branch you'd already picked in the "Use workflow from" selector. Removed it entirely — the workflow now has zero inputs and just deploys `github.sha`, the commit at the tip of whichever branch was picked in that selector.
- Ingredient quantities on the recipe detail and meal plan review pages showed awkward decimals (e.g. "0.33 cup") instead of the fractions recipes are actually written in. Added `UnitConverter.ToFractionString()`, which matches the fractional part against common cooking fractions (halves, quarters, eighths, thirds, sixths) and renders "1/3", "1 1/2", etc., falling back to the old trimmed-decimal format when the value isn't close to a recognizable fraction. (#31)
- Ingredient quantity/unit text on the recipe detail view (and Cook Mode sheet) wrapped onto two lines for anything longer than ~8 characters (e.g. "1 Tablespoon", and — after the fraction-display change above — increasingly common combos like "1 1/2 Cups"), breaking row height and pushing the ingredient name out of line. The column's fixed `width: 76px` (added for issue #30 to keep names aligned to a column) was never widened to fit the longer fraction strings. Widened it to 120px, measured against realistic quantity/unit combinations, and added `white-space: nowrap` as a safety net for rare longer cases — keeps the issue #30 alignment fix intact while eliminating the wrapping. (#30)

## 2026-07-25

### Added

- Initial `CHANGELOG.md` to track notable changes going forward.

### Baseline

Feature set at the time this changelog was introduced (not individually
dated — see git history for prior detail):

- Recipe management (create, edit, organize with ingredient sections and instructions)
- Recipe import from URL via Spoonacular, with AI-assisted ingredient parsing
- Weekly meal planning with ingredient aggregation and unit conversion
- Kroger integration: product search, UPC linking, cart add via OAuth
- Shopping list with aisle sorting, price lookup, and Kroger store detection
- PWA support with service worker caching
- OpenTelemetry traces/metrics/logs exported to Grafana Cloud
