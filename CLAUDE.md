# RecipeHelper — Claude Context

## What this is
Single-user ASP.NET Core 8 MVC app for recipe management, weekly meal planning, and Kroger grocery cart integration. Deployed on a Hetzner VM at `https://sutherlinsrecipes.duckdns.org`. Primary device is a mobile phone.

## Commands

```bash
# Build (run from RecipeHelper/ subdirectory)
dotnet build

# Build CSS (Tailwind — must run from RecipeHelper/ where package.json lives)
npm run css:build

# EF migrations (from RecipeHelper/)
dotnet ef migrations add <Name>
dotnet ef database update

# Deploy to Hetzner VM (run from repo root, requires git bash + ~/.ssh/hetzner key)
bash deploy/deploy.sh

# GitHub CLI (installed at C:\Program Files\GitHub CLI\gh.exe)
# PATH may need updating in new shells: $env:PATH += ";C:\Program Files\GitHub CLI"
# IMPORTANT: use --body-file (write body to temp file first) — inline --body fails in
# PowerShell when the body contains backtick-quoted code (e.g. `rounded-full`)
gh pr create --title "..." --body-file path/to/body.md --base main
gh pr list

# Merging: always delete the source branch on merge (--delete-branch), whether
# via gh or the merge_pull_request tool — don't leave merged branches around.
gh pr merge <number> --squash --delete-branch
```

## Architecture

**Flow:** Recipes → Meal Plan (weekly) → Ingredient aggregation → Review → Kroger shopping list/cart

**External APIs:**
- **Spoonacular** — recipe import (`ImportController`, `SpoonacularService`, `ImportService`)
- **Kroger** — product search, cart add (`KrogerService`, `KrogerAuthService`)
  - Auth is OAuth2 client-credentials; token cached in `KrogerAuthService`
  - Product details use `/products/{upc}?filter.locationId=...` (NOT `/products?filter.productId=...` — the latter silently ignores locationId and returns no aisle data)
  - Store ID comes from `KrogerLocationId` cookie (set via location services in Settings)
  - User's preferred store: Mariemont `01400421`

**DB:** Azure SQL Server (`sql-recipe-helper.database.windows.net`, `germanywestcentral`) via EF Core. `DatabaseContext` in project root. Auth via Entra service principal — connection string uses `Authentication=Active Directory Service Principal`.

**Blob Storage:** Azure Storage Account `sarecipehelper` (`germanywestcentral`), container `recipe-images`. Auth via `ClientSecretCredential` using `AzureAd` config section (no connection string in prod). `StorageService` falls back to connection string when `StorageSettings:connectionString` is present (local dev only).

**CSS:** Tailwind via `npm run css:build` → `wwwroot/css/output.css`. Never hand-edit output.css.

**PWA / Service Worker (`wwwroot/sw.js`):** caches static assets (cache-first) and page navigations (stale-while-revalidate), keyed by `CACHE_VERSION`. **This is stamped automatically to the deployed commit SHA by both `deploy/deploy.sh` and `.github/workflows/deploy.yml` at publish time** — every deploy gets a new version and old caches are evicted on activate, so you never need to hand-edit `CACHE_VERSION` (the `'dev'` literal in source only applies to local `dotnet run`). The new worker doesn't take over automatically (`skipWaiting()` is gated behind a user tap, not called unconditionally on install) — `site.js` shows a "tap to refresh" banner via `updatefound` and posts `SKIP_WAITING` when tapped, so an already-open tab isn't swapped to a new version mid-session.

## Key Files

| File | Purpose |
|---|---|
| `Controllers/DinnerController.cs` | Meal plan index, add/remove entries, ingredient review |
| `Controllers/ShoppingListController.cs` | Shopping list CRUD, aisle sort, Kroger price lookup |
| `Controllers/CartController.cs` | Kroger cart add flow |
| `Services/MealPlanService.cs` | Week-start math, AddEntryAsync / RemoveEntryAsync |
| `Services/KrogerService.cs` | Product search + cart add; `ConvertIngredientsToCartItems` handles unit→pack conversion |
| `Utility/UnitConverter.cs` | Canonical unit parsing/conversion; base units: teaspoons (volume), grams (weight) |
| `Utility/DensityTable.cs` | Volume↔weight conversion via ingredient densities |
| `Utility/KrogerSizeParser.cs` | Parses Kroger size strings like "8 ct / 22 oz" |
| `Models/Kroger/KrogerPackInfo.cs` | Parsed pack info + soldBy inference |
| `Utility/MeasurementHelper.cs` | Thin wrapper around `UnitConverter.Parse` + `ToDisplayName` |
| `Controllers/ImportController.cs` | Recipe import flow: URL fetch → Spoonacular preview → mapping page → save |
| `Services/ImportService.cs` | Saves mapped import to DB; **only `SelectedUpc` is persisted** — `SuggestedUpc` is a UI hint only |
| `Services/StorageService.cs` | Blob upload/delete; uses `ClientSecretCredential` in prod, connection string in dev |
| `Program.cs` | DI registration + OpenTelemetry wiring (traces / metrics / logs → Grafana Cloud OTLP) |
| `deploy/deploy.sh` | Full deploy: CSS build → dotnet publish → scp → restart systemd |
| `wwwroot/sw.js` | Service worker: static-asset + page caching; `CACHE_VERSION` auto-stamped at deploy time (see PWA note above) |
| `wwwroot/js/site.js` | SW registration + "update available" reload banner; also global loading-overlay wiring |

## Data Model

```
MealPlan { Id, WeekStartDate, CreatedUtc, Entries[] }
MealPlanEntry { Id, MealPlanId, RecipeId, DayOfWeek }   // 0=Mon … 6=Sun
```

Multiple entries per `(MealPlanId, DayOfWeek)` are allowed and expected (dinner + sides). No unique index on that pair.

**Measurement.Name DB values:** `"Cups"`, `"Teaspoons"`, `"Tablespoons"`, `"Ounces"`, `"Pounds"`, `"Grams"`, `"Unit"`

`UnitConverter.Parse` handles both these names and common shorthand (tsp, oz, g, etc.).

## Changelog

- **`CHANGELOG.md`** (repo root) tracks notable changes, grouped by date (no version tags — this app deploys continuously).
- **Update it as part of every change**, not as an afterthought: add an entry under `## [Unreleased]` in the same commit/PR that makes the change, then move it under today's dated heading when deployed. Group entries under `### Added` / `### Changed` / `### Fixed` / `### Removed` per [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
- Skip purely internal refactors, formatting-only diffs, and dependency bumps with no user-facing effect.

## Bug-Fix Verification

Before opening a PR that claims to fix a bug, actually prove it — narrative plausibility is not verification. (Issue #34's first fix attempt shipped a technically-sound-sounding "iOS WebKit fails to re-establish scroll/compositing layers" explanation that was never true; the real bug was a one-line CSS/JS logic error that a 10-minute repro would have caught.)

- **Reproduce before fixing.** Don't propose a fix from reading code alone. Make the bug happen on demand first — a failing test, or a runnable repro — then apply the fix, then confirm the same repro now passes. A PR whose only "verification" is an unchecked checklist for a human to run later is not verification.
- **If the full app can't run in this sandbox** (no DB access, no .NET SDK, whatever), don't skip straight to "please verify manually" — isolate the affected HTML/CSS/JS into a minimal standalone harness and verify it with Playwright. This works for plain front-end logic bugs even without a browser engine matching the reported platform (a Chromium repro is still real evidence unless the bug is proven engine-specific).
- **Be suspicious of "known platform quirk" explanations you can't test.** They sound sophisticated and are hard to argue with, which is exactly why they let unverified fixes through. Prefer a mechanistic trace over a folklore explanation: for a value that's wrong at runtime, trace who sets it, who clears it, and what it falls back to when cleared — that trace alone tends to surface the actual bug.
- **Put the evidence in the PR**, not just the plan: what repro was run, and what it showed before vs. after the fix.

## Coding Conventions

- **Timezone:** User is America/New_York (EDT). Always use `MealPlanService.LocalToday()` for "today" — never raw `DateTime.UtcNow` in user-facing date logic (server is on Hetzner/UTC).
- **Mobile-first UI:** Design for ~375px first. Max one prominent action per header row. Touch targets ≥ 44px (`py-3` minimum). Fixed bottom bars use `bottom-16` to clear mobile browser chrome.
- **Server-as-truth JS pattern:** Meal plan JS posts to server and re-renders from the JSON response — no optimistic updates.
- **Ingredient aggregation:** `SubmitDinnerSelections` groups recipe IDs by count and expands each row N times before summing — so a recipe on 2 days = 2× its ingredients.
- **Antiforgery:** JS fetches send token via `RequestVerificationToken` header (not form field). Token read from `input[name="__RequestVerificationToken"]`.

## Meal Plan UI

- `Dinner/Index` — weekly grid (Mon–Sun), always-interactive day cards, autosave on pick/remove
- Each day card: stack of entry rows (image + name + × remove) + "Add recipe" button below; supports multiple entries per day
- Today's card: `border-red-200`, red day name + date — computed with `MealPlanService.LocalToday()`
- Each card shows the actual calendar date (e.g., "May 26") — `weekStart.AddDays(d).ToString("MMM d")`
- Picker overlay: `z-[200]`, sits above nav (`z-50`) and loading overlay (`z-[100]`)
- `AddDayRecipe` / `RemoveEntry` both return `{ planId, entries: [{entryId, dayOfWeek, recipeId, name, img}] }`
- JS `renderAll(data)` repaints all 7 day containers from this response; card dims to 55% opacity during in-flight requests
- Font: Inter (Google Fonts), applied globally via `<body style="font-family: 'Inter', sans-serif;">`

## Import UI

- `Import/ImportRecipe` — URL input → Spoonacular fetch → read-only preview + "Review & Save" button
- `Import/MappedImportedRecipe` — ingredient mapping page; each ingredient maps to a Kroger product
- **Binary mapped state:** cards are either "Not mapped" (gray, no image) or explicitly mapped (product image + name + "Remove" button). `SuggestedUpc` is **never** shown on the card — it only pre-fills the modal's "Recommended" pinned item when the user opens the picker.
- Mapping is optional; user can confirm without mapping any ingredients
- JS selectors: `.js-row`, `.js-include`, `.js-selected-upc`, `.js-selected-name`, `.js-selected-source`, `.js-collapsible`, `.js-open-map`, `.js-exclude-btn`, `.js-clear-selection`, `.js-modal-clear`
- Modal z-index: `z-[200]` (above nav `z-50` and loading overlay `z-[100]`)
- Amount inputs use `step="any"` and `inputmode="decimal"` — Spoonacular returns fractional quantities (1/3, 1/6) that fail `step="0.01"` browser validation and silently block form submission on mobile
- Spoonacular `originalName` sometimes includes the raw quantity string (e.g. "30 g of sour cream") — `StripLeadingQuantity` in `FromSpoonacular()` strips these before display

## UI Testing

**After any UI change** (HTML structure, CSS, JS interactions, layout), use Playwright to verify the result on a mobile viewport before committing. This app is used exclusively on iPhone, so desktop-only testing is not sufficient.

### Setup (first time, from `RecipeHelper/`)

```bash
npm install --save-dev @playwright/test
npx playwright install chromium
```

Create `RecipeHelper/playwright.config.ts`:

```ts
import { defineConfig, devices } from '@playwright/test';
export default defineConfig({
  use: { baseURL: 'https://localhost:7127' },
  projects: [{ name: 'iPhone 14', use: { ...devices['iPhone 14'] } }],
  webServer: {
    command: 'dotnet run',
    url: 'https://localhost:7127',
    reuseExistingServer: true,
    ignoreHTTPSErrors: true,
  },
});
```

### Running

```bash
# From RecipeHelper/
npx playwright test           # headless
npx playwright test --ui      # interactive UI mode (recommended for visual review)
```

### iOS checklist — verify after every UI change

- **Safe area**: content not obscured by iPhone status bar or home indicator
- **Tab bar**: all tabs tappable, correct active highlight, center `+` FAB opens action sheet
- **Nav bar**: only renders when there's a title, back button, or right action; absent on Recipes/Meal Plan pages
- **Sticky elements**: stick at the right offset (`sticky-below-status` when no nav bar, `sticky-below-nav` when nav bar present)
- **Touch targets**: all interactive elements ≥ 44px tall — confirm taps register
- **Action sheets / modals**: animate in/out correctly, dismiss on backdrop tap and Cancel
- **z-index layers**: overlays sit above nav (`z-50`) and loading overlay (`z-[100]`); pickers/modals at `z-[200]`+
- **Navigation**: tab switches, back links, form submissions all route correctly on mobile

## Deployment

- **Host:** Hetzner VPS, `178.105.73.57`
- **SSH key:** `~/.ssh/hetzner`
- **Service:** systemd unit `recipehelper`, app root `/var/www/recipehelper`
- **Public URL:** `https://sutherlinsrecipes.duckdns.org`
- Deploy script handles: CSS build → publish linux-x64 → scp to `/tmp/recipehelper/` → stop/copy/start service
- **CI deploy:** `.github/workflows/deploy.yml` does the same build and pushes it to prod over a restricted, non-root `deploy` SSH user — see `deploy/remote/README.md` for the one-time VM setup and required `DEPLOY_SSH_KEY` secret. **Fires automatically** on every PR open/push targeting `main` that touches source code (deploys that PR's head commit — every PR branch is live on production the moment you push a code change to it, no manual step). Docs-only commits (`**.md`) are skipped via `paths-ignore` and don't trigger a redeploy. Also runnable manually anytime from the Actions tab (works fine from the GitHub mobile app) — no inputs, deploys whichever branch is picked in the "Use workflow from" selector, e.g. to redeploy `main` itself or a branch with no open PR. A newer push cancels an in-flight deploy of a now-stale commit (`cancel-in-progress: true`) rather than queuing behind it. Guarded against forked PRs ever running with this repo's deploy secrets.
- **Hetzner Cloud Firewall:** SSH (22) is restricted by source IP. If `bash deploy/deploy.sh` fails with a connection timeout, the home IP probably rotated — whitelist the current one at `https://api.ipify.org` in the Hetzner Cloud console firewall.
- **`appsettings.json` and `appsettings.Production.json` are both gitignored.** `appsettings.json` contains empty placeholders only. All secrets live in `appsettings.Production.json` on the dev machine, which ships to the VM via `dotnet publish` (SDK auto-copies all `appsettings*.json` as content). Treat `appsettings.Production.json` as the production-secrets source of truth.
- **Entra service principal:** `sp-recipe-helper-p` (client ID `3e54accb-87f2-4f61-9732-9d01bf5c669d`, object ID `6922cf3d-d918-47fa-ac48-9e72ffa1378e`). Has `db_datareader`, `db_datawriter`, `db_ddladmin` on `recipehelper` DB and `Storage Blob Data Contributor` on `sarecipehelper`. Credentials in `AzureAd` config section.
- **Known issue: ephemeral data protection keys.** The app uses in-memory key storage, so antiforgery tokens are invalidated on every restart (deploy). Users see a blank page on the first POST after a deploy and must go back and retry. Fix: persist keys to disk or blob storage via `AddDataProtection().PersistKeysTo...()` in `Program.cs`.
- **.NET package pinning on the VM.** The VM has both Microsoft's official apt repo (`packages.microsoft.com`) and Ubuntu's own repo (`jammy-updates`/`jammy-security`) providing `dotnet-runtime-8.0`/`aspnetcore-runtime-8.0` at the *same version string* but different install layouts — Microsoft's build installs to `/usr/share/dotnet` (where `/usr/bin/dotnet` and the systemd unit expect it), Ubuntu's installs to `/usr/lib/dotnet`. If Ubuntu's wins an `apt upgrade` (it did once, silently, and broke `recipehelper.service` with "No frameworks were found"), the fix is `apt-get install --allow-downgrades aspnetcore-runtime-8.0=<ver>-1 dotnet-runtime-8.0=<ver>-1 dotnet-hostfxr-8.0=<ver>-1` (the `-1` suffix is Microsoft's build, vs. `-0ubuntuN` for Ubuntu's). `/etc/apt/preferences.d/dotnet-microsoft-repo` now pins `dotnet-*`/`aspnetcore-*` to `origin packages.microsoft.com` to prevent recurrence.

## Observability

- **Stack:** OpenTelemetry SDK → OTLP/HTTP → Grafana Cloud (free tier).
- **Wired in `Program.cs`** — traces (ASP.NET, HttpClient, SqlClient), metrics (ASP.NET, HttpClient, runtime), and logs all export over OTLP.
- **Config precedence:** `OpenTelemetry:*` section in appsettings first, then `OTEL_*` env-var fallback. Prod uses `appsettings.Production.json`; local dev uses env vars set in `Properties/launchSettings.json`.
- **Critical: per-signal path is appended manually.** `ConfigureOtlp(opts, signalPath)` writes `{base}/v1/{signal}` to `opts.Endpoint`. The OTel .NET SDK does NOT auto-append `/v1/traces` etc. when the endpoint is set programmatically (only when read from the `OTEL_EXPORTER_OTLP_ENDPOINT` env var by the SDK itself). Without this, Grafana's gateway silently drops metrics + traces.
- **Service identity:** `service.name=recipe-helper`, `deployment.environment=Production|Development`.
- **VM host metrics (CPU/mem/disk) are NOT covered** by the app instrumentation. Use Grafana Cloud → Connections → Integrations → "Linux Server" (installs Alloy on the VM) when needed.
