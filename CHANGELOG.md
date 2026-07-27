# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project is deployed continuously (no version tags), so entries are grouped
by date rather than by release version.

## [Unreleased]

### Added

- Manual "Deploy to Production" GitHub Actions workflow (`workflow_dispatch`, runnable from the GitHub mobile app), reaching the VM over Tailscale via OIDC federated identity so no long-lived secret is stored in GitHub.
- Deploy workflow now also fires automatically on every PR open/push targeting `main` that touches source code, deploying that PR's head commit so it's live for on-device testing without a manual run. Docs-only commits (`**.md`) are skipped and don't trigger a redeploy. A newer push cancels an in-flight deploy of a now-stale commit rather than queuing behind it, and forked PRs are blocked from ever triggering a deploy with this repo's secrets.
- Service worker `CACHE_VERSION` is now auto-stamped to the deployed commit SHA by both `deploy/deploy.sh` and the GitHub Actions deploy workflow, so every deploy evicts old PWA caches automatically instead of relying on someone remembering to hand-bump the version.
- "Update available — tap to refresh" banner (`site.js`) when a new service worker finishes installing. `sw.js` no longer calls `skipWaiting()` unconditionally on install, so an already-open tab isn't silently swapped to a new version mid-session — the user chooses when to reload.
- A brief scale "pop" on the target day card when dragging a meal plan entry crosses into a new day, as a visual stand-in for haptic feedback — iOS Safari doesn't expose the Vibration API (or any haptics API) to web content, so this is the closest substitute available to a PWA. (#47)

### Changed

- Photo import's recipe extraction (`IngredientsService.ExtractRecipeFromNormalizedPhotosAsync`) now uses `gpt-5.4` instead of `gpt-4o` for the vision call, aiming for more accurate ingredient/quantity extraction from recipe photos.

### Fixed

- Meal plan entry rows (`.entry-row`) and their drag-ghost clone had no `user-select`/`-webkit-touch-callout` rule, so iOS's native text-selection/callout gesture competed with the long-press-then-slide move gesture in `bindMoveGestures`, making it hard to complete a drag. Added `user-select: none` / `-webkit-user-select: none` / `-webkit-touch-callout: none` to both classes. (#47)
- Dragging a meal plan entry to a new day highlighted two different boxes at once (the whole day card, and a tighter box around just the entry rows), flickering jerkily as the drag moved. Both the day card and its inner `.day-entries` wrapper carried the same `data-day` attribute, so `closest('[data-day]')` inconsistently matched whichever was nearer depending on where the finger was. Gave the outer card its own `.day-card` class and scoped all drag-target/dim lookups to it. (#47)
- Recipe picker sheet on Meal Plan losing its header, search bar, and backdrop on reopen: `max-height: 88svh` was only ever set as an inline style, so `closePicker()`'s `pickerCard.style.maxHeight = ''` reset wiped it permanently instead of falling back to a base value. Moved the 88svh cap into the `#recipePickerCard` CSS rule (same pattern already used for the keyboard-open 72svh override) so clearing the inline override correctly restores it. (#34)
- The above picker fix wasn't reaching installed PWAs because the Meal Plan page is served via `staleWhileRevalidate` and the service worker cache version hadn't changed — fixed by the auto-stamping change above.
- Deploy workflow originally took a `ref` text input that duplicated the branch you'd already picked in the "Use workflow from" selector. Removed it entirely — the workflow now has zero inputs and just deploys `github.sha`, the commit at the tip of whichever branch was picked in that selector.
- Ingredient quantities on the recipe detail and meal plan review pages showed awkward decimals (e.g. "0.33 cup") instead of the fractions recipes are actually written in. Added `UnitConverter.ToFractionString()`, which matches the fractional part against common cooking fractions (halves, quarters, eighths, thirds, sixths) and renders "1/3", "1 1/2", etc., falling back to the old trimmed-decimal format when the value isn't close to a recognizable fraction. (#31)
- Ingredient quantity/unit text on the recipe detail view (and Cook Mode sheet) wrapped onto two lines for anything longer than ~8 characters (e.g. "1 Tablespoon", and — after the fraction-display change above — increasingly common combos like "1 1/2 Cups"), breaking row height and pushing the ingredient name out of line. The column's fixed `width: 76px` (added for issue #30 to keep names aligned to a column) was never widened to fit the longer fraction strings. Widened it to 120px, measured against realistic quantity/unit combinations, and added `white-space: nowrap` as a safety net for rare longer cases — keeps the issue #30 alignment fix intact while eliminating the wrapping. (#30)
- Photo import's "Take Photo" option could only ever hold one photo: iOS's native file picker replaces `<input type="file">`'s entire file list on every invocation (camera or library), so capturing a second shot silently wiped out the first instead of adding to it. The photo picker now tracks selections in JS state and merges each new pick into it instead of treating the picker's latest result as the whole selection.
- Photo import rejected any standard image (JPEG/PNG/WebP) over 15 MB, forcing users to compress full-resolution phone photos by hand before importing. Oversized standard images are now auto-resized (max 2000px) and recompressed (JPEG quality 88) server-side instead of being rejected — the same approach already used for DNG conversion.
- Meal plan ingredient review showed the same ingredient as separate rows (e.g. two "Minced Garlic" lines) instead of one merged entry. `SubmitDinnerSelections` aggregated by canonical `Ingredient.Id`, so two recipes' ingredients that resolve to different DB rows for the same display name (a canonicalization gap) never merged even when using the same unit. Aggregation now groups by normalized name instead, so same-unit duplicates truly merge into one line. When an ingredient genuinely has incompatible units across recipes (e.g. a bare count vs. a volume amount — there's no "cloves" measurement in this app's schema, so those import as a unitless count), it still can't be summed into one number, but its rows are now guaranteed adjacent in the list (explicit sort by section/name) instead of scattered whichever way `Dictionary` iteration happened to land.
- Creating or editing a recipe with an image upload could throw an unhandled `NullReferenceException` (blank screen in production) if the Blob Storage upload failed: `StorageService.StoreRecipeImage` already caught upload exceptions and returned `null`, but `RecipeService.CreateRecipe`/`UpdateRecipeAsync` dereferenced the result unconditionally (`blobResponse.BlobUri`). Both call sites now null-check the response, log a warning, and save the recipe without changing its image instead of crashing. (#56)
- `StorageService.StoreRecipeImage`'s catch block logged blob upload failures at `Information` level, so a genuine auth/permission failure (as opposed to the "expected" null-check case above) was invisible in Grafana instead of alerting anyone. Now logs at `Error` with the exception and blob name.
- Deploy workflow originally took a `ref` text input that duplicated the branch you'd already picked in the "Use workflow from" selector, plus the SSH key rotation, nologin-shell, and .NET repo-pinning fixes needed to get it working end-to-end (see `deploy/remote/README.md` for details).

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
