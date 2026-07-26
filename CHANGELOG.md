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

### Fixed

- Recipe picker sheet on Meal Plan losing its header, search bar, and backdrop on reopen: `max-height: 88svh` was only ever set as an inline style, so `closePicker()`'s `pickerCard.style.maxHeight = ''` reset wiped it permanently instead of falling back to a base value. Moved the 88svh cap into the `#recipePickerCard` CSS rule (same pattern already used for the keyboard-open 72svh override) so clearing the inline override correctly restores it. (#34)
- The above picker fix wasn't reaching installed PWAs because the Meal Plan page is served via `staleWhileRevalidate` and the service worker cache version hadn't changed — fixed by the auto-stamping change above.

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
