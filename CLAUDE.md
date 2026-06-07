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

## Data Model

```
MealPlan { Id, WeekStartDate, CreatedUtc, Entries[] }
MealPlanEntry { Id, MealPlanId, RecipeId, DayOfWeek }   // 0=Mon … 6=Sun
```

Multiple entries per `(MealPlanId, DayOfWeek)` are allowed and expected (dinner + sides). No unique index on that pair.

**Measurement.Name DB values:** `"Cups"`, `"Teaspoons"`, `"Tablespoons"`, `"Ounces"`, `"Pounds"`, `"Grams"`, `"Unit"`

`UnitConverter.Parse` handles both these names and common shorthand (tsp, oz, g, etc.).

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

## Deployment

- **Host:** Hetzner VPS, `178.105.73.57`
- **SSH key:** `~/.ssh/hetzner`
- **Service:** systemd unit `recipehelper`, app root `/var/www/recipehelper`
- **Public URL:** `https://sutherlinsrecipes.duckdns.org`
- Deploy script handles: CSS build → publish linux-x64 → scp to `/tmp/recipehelper/` → stop/copy/start service
- **Hetzner Cloud Firewall:** SSH (22) is restricted by source IP. If `bash deploy/deploy.sh` fails with a connection timeout, the home IP probably rotated — whitelist the current one at `https://api.ipify.org` in the Hetzner Cloud console firewall.
- **`appsettings.json` and `appsettings.Production.json` are both gitignored.** `appsettings.json` contains empty placeholders only. All secrets live in `appsettings.Production.json` on the dev machine, which ships to the VM via `dotnet publish` (SDK auto-copies all `appsettings*.json` as content). Treat `appsettings.Production.json` as the production-secrets source of truth.
- **Entra service principal:** `sp-recipe-helper-p` (client ID `3e54accb-87f2-4f61-9732-9d01bf5c669d`, object ID `6922cf3d-d918-47fa-ac48-9e72ffa1378e`). Has `db_datareader`, `db_datawriter`, `db_ddladmin` on `recipehelper` DB and `Storage Blob Data Contributor` on `sarecipehelper`. Credentials in `AzureAd` config section.
- **Known issue: ephemeral data protection keys.** The app uses in-memory key storage, so antiforgery tokens are invalidated on every restart (deploy). Users see a blank page on the first POST after a deploy and must go back and retry. Fix: persist keys to disk or blob storage via `AddDataProtection().PersistKeysTo...()` in `Program.cs`.

## Observability

- **Stack:** OpenTelemetry SDK → OTLP/HTTP → Grafana Cloud (free tier).
- **Wired in `Program.cs`** — traces (ASP.NET, HttpClient, SqlClient), metrics (ASP.NET, HttpClient, runtime), and logs all export over OTLP.
- **Config precedence:** `OpenTelemetry:*` section in appsettings first, then `OTEL_*` env-var fallback. Prod uses `appsettings.Production.json`; local dev uses env vars set in `Properties/launchSettings.json`.
- **Critical: per-signal path is appended manually.** `ConfigureOtlp(opts, signalPath)` writes `{base}/v1/{signal}` to `opts.Endpoint`. The OTel .NET SDK does NOT auto-append `/v1/traces` etc. when the endpoint is set programmatically (only when read from the `OTEL_EXPORTER_OTLP_ENDPOINT` env var by the SDK itself). Without this, Grafana's gateway silently drops metrics + traces.
- **Service identity:** `service.name=recipe-helper`, `deployment.environment=Production|Development`.
- **VM host metrics (CPU/mem/disk) are NOT covered** by the app instrumentation. Use Grafana Cloud → Connections → Integrations → "Linux Server" (installs Alloy on the VM) when needed.
