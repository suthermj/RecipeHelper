# Multi-User Architecture — Future State

**Status:** Planning only. Nothing here is implemented. This document exists so the
architecture and data flows can be reviewed thoroughly before any code changes —
per explicit request, this is a design reference, not a task list to execute yet.

**Revised 2026-10-24:** ownership moved from per-user (`OwnerId`) to
**per-household** (`HouseholdId`), so partners can share and co-edit everything.
Identity approach, signup model and membership rules are now decided (see
[Decisions](#decisions)).

## Goal

Transform RecipeHelper from a single global-tenant app into a real multi-user app:

- Users sign in with their own account.
- Users belong to a **household** (exactly one). A household's Recipes, Meal Plans,
  Shopping Lists and store preference are shared by all its members, who edit them
  together (e.g. you and your fiancé).
- A brand-new household starts blank.
- Recipes are **private to the household** by default; a household can explicitly
  mark one **Public**.
- A new **Discover** tab shows Public recipes from all households, read-only, and
  viewable **without signing in**.
- From Discover, a signed-in user can **copy** a public recipe into their own
  household to edit it — copying, not live-syncing.
- Separately: whether to move off Azure SQL (cost) is worth evaluating, but is not
  a prerequisite.

## Decisions

| Topic | Decision |
|---|---|
| Identity | Hand-rolled: `Users` table + built-in `PasswordHasher<User>` + cookie auth. Not full ASP.NET Core Identity. |
| Signup | Invite-only via **invite links**. Email delivery of invites comes later. |
| Opening signup later | Cheap: all account creation goes through one `UserService.CreateAsync`. Opening up = add `/Account/Register` calling it (plus rate limiting and email verification). No schema change. |
| Membership | **Exactly one household per user, never null.** A solo household is auto-created at signup, so no query handles "no household". Using the app alone is a household of one. |
| Ownership | Household owns Recipes, MealPlans, ShoppingLists (and DraftRecipes). Recipes also carry `CreatedByUserId` for attribution. |
| Kroger | Cart token stays **per user** (a Kroger login belongs to a person); store preference moves to the household. |
| Public recipes | Viewable anonymously (Discover actions are `[AllowAnonymous]`). Lands in the Discover phase. |

### Household-owned recipes: trade-offs accepted

- No personal-only recipes; any member can edit or delete any household recipe. A
  per-recipe visibility flag can be added later without reshaping the model.
- Leaving a household: recipes stay with the household. Leavers get a "copy my
  recipes out" step — the same deep-clone as Discover's copy, built once.
- Joining a household with existing data: an empty solo household is deleted; a
  non-empty one needs an explicit merge step (re-point rows to the new `HouseholdId`).
- Discover shows the household name and, via `CreatedByUserId`, the author.

## Current State (baseline, as of this writing)

- **No app-level identity.** No `[Authorize]` anywhere, no `AddAuthentication` /
  `app.UseAuthentication()` in `Program.cs`, no `Users` table. Anyone who can reach
  the URL can use the app.
- The only "auth" in the codebase is `AuthController`, which does **Kroger's**
  OAuth for cart access (`auth/login`, `auth/callback`) — it authenticates the
  browser to Kroger's API, not to RecipeHelper. It stores `KrogerCustomerToken`
  keyed by Kroger's own `KrogerProfileId` and drops a `KrogerProfileId` cookie.
  User login therefore lives under `/Account/*` to avoid the route collision.
- Store preference (`KrogerLocationId`) is a plain browser cookie, not linked to
  any account.
- **All data is global.** `Recipe`, `MealPlan`, `ShoppingList`, `Ingredient`,
  `KrogerProduct`, etc. have no owner column — see `DATABASE.md` for the full
  current schema. Zero query in the app filters by "whose data is this."
- Blob storage (`recipe-images` container) uses flat, unscoped filenames.
- Data-protection keys already persist to `/var/lib/recipehelper/keys`, so auth
  cookies will survive deploys.

## Target Data Model

### New: `User`, `Household`, `HouseholdInvite`

- `User { Id, Email (unique), DisplayName, PasswordHash, CreatedUtc, IsAdmin, HouseholdId (required FK) }`
- `Household { Id, Name, CreatedUtc }`
- `HouseholdInvite { Id, HouseholdId, TokenHash, CreatedByUserId, ExpiresUtc, UsedUtc, UsedByUserId }`
  — single-use, expiring (7 days), token stored hashed.
- Cookie authentication (`app.UseAuthentication()` plus a `RequireAuthenticatedUser`
  fallback policy) — consistent with a mobile PWA with no API/native clients.

### `Recipe` — household-owned + visibility-flagged

| Column | Type | Notes |
|---|---|---|
| `HouseholdId` | int, FK → `Household.Id` | Required. Who owns this recipe. |
| `CreatedByUserId` | int, FK → `User.Id` | Attribution (Discover, "added by"). |
| `IsPublic` | bool, default `false` | Private unless the household flips it. Discover phase. |
| `ClonedFromRecipeId` | int?, FK → `Recipe.Id` | Provenance when copied from Discover. |

`RecipeIngredient`, ingredient sections and images stay child records of `Recipe`
and inherit scope through the `RecipeId` FK.

### `MealPlan`, `ShoppingList`, `DraftRecipe` — household-owned

Add `HouseholdId` (FK → `Household.Id`). `MealPlanEntry` / `ShoppingListItem`
inherit scope through their parent FK, same as today.

### `KrogerCustomerToken` — link to the app user

Add `UserId` (FK → `User.Id`): "this app account" → "which Kroger account they
connected." Per person, not per household. The store (`KrogerLocationId`) moves
from a cookie to the household.

### Stays global (shared reference data, not user content)

- `Ingredient`, `KrogerProduct`, `IngredientKrogerProduct`, `Measurement`.

See Open Questions for the per-household product-preference nuance. `Product`
(legacy) is flagged unused in `DATABASE.md` — remove rather than migrate.

## Query-Scoping Audit

This is the actual bulk of the work, not the schema change. Every call site below
needs an explicit "does this need `WHERE HouseholdId == current`" review:

| File | Raw `_context.Recipes` / `MealPlans` / `ShoppingLists` call sites |
|---|---|
| `Services/ShoppingListService.cs` | 8 |
| `Services/MealPlanService.cs` | 6 |
| `Controllers/RecipeController.cs` | 5 |
| `Controllers/DinnerController.cs` | 3 |
| `Services/RecipeService.cs` | 2 |
| `Services/ImportService.cs` | 1 |

(Snapshot count — re-grep before starting, this will drift.)

Introduce an `ICurrentUser` scoped service (via `IHttpContextAccessor` reading the
authenticated `ClaimsPrincipal`) exposing `UserId` and `HouseholdId`, and thread it
through services. By-id lookups must also filter by household (404 for another
household's ids). Consider an EF **global query filter** on the owned entities so
scoping is the default rather than something to remember.

**The one deliberate exception:** Discover queries read *across* households
(`WHERE IsPublic == true`). Isolate this into its own clearly-named service method
(using `IgnoreQueryFilters()` if filters are adopted) so it is the *only* place
allowed to query across households.

## Discover Tab & Copy Flow (later phase)

- `DiscoverController` / `Views/Discover/Index.cshtml` — cards for `IsPublic`
  recipes across all households. Read-only, anonymous-viewable; similar shape to
  the existing read-only meal-plan share page. Shows household name / author.
- **"Copy to My Recipes"** (requires sign-in): deep-clones the `Recipe` and its
  `RecipeIngredient` rows with new keys, `HouseholdId = current`,
  `CreatedByUserId = current`, `IsPublic = false`, `ClonedFromRecipeId` set. Kroger
  mappings (`SelectedKrogerUpc`) carry over as-is (references into the global
  `KrogerProduct` table).
- **Image:** leaning toward copying the blob at clone time, to avoid dangling
  references if the original is replaced or deleted.
- "Make public" switch on the recipe edit page, off by default.
- Clones are fully independent after copying — no live-sync, by design.

## Migration of Existing Data

Create one seed admin user with a household, and in the migration that adds
`HouseholdId` to `Recipe` / `MealPlan` / `ShoppingList`, backfill every existing
row to that household (single step, not nullable-then-backfill-then-required,
unless a longer window turns out to be needed). The seed admin comes from
`Seed:AdminEmail` / `Seed:AdminPassword` config on first boot when `Users` is
empty; remove the password config afterwards.

## Database Engine (separate decision, its own track)

Raised because Azure SQL Database carries an ongoing cost independent of the
Hetzner VPS already being paid for either way. Not decided — explicitly **not** a
prerequisite for the work above:

- **Don't split storage across database technologies.** The schema is thoroughly
  relational — FKs, cascading deletes, a filtered unique index on
  `MealPlan.ShareToken` — and multi-user adds *more* relational structure. Two
  engines means two systems to back up, secure and operate for no benefit.
- **Cosmos DB is a poor fit** — document/NoSQL store, and this schema leans on
  joins and cascades throughout.
- **The real cost lever: self-hosted Postgres on the existing Hetzner VM.** Drops
  the Azure SQL bill to $0. EF Core's Npgsql provider is mature, migrations
  regenerate cleanly, and nothing in the schema is SQL-Server-exotic (the
  `HasFilter` partial index translates to a Postgres partial index). Costs:
  connection auth moves from `Authentication=Active Directory ...` to
  Postgres-native credentials (now naturally stored in Key Vault, which the VM
  reads via its Arc identity); Azure SQL's managed backups/PITR go away, so
  backups become a self-owned `pg_dump` cron (e.g. to the existing blob account).
  A cutover with a maintenance window, not a live migration.
- **Middle ground:** Azure Database for PostgreSQL (Flexible Server, Burstable) —
  same EF migration effort, usually cheaper than Azure SQL, keeps managed backups.
  Compare prices against the current tier before choosing.
- Can happen before, after, or independently of the multi-user work. Recommended:
  keep fully separate.

## Open Questions

1. **Per-household Kroger product preference.** Should
   `IngredientKrogerProduct.IsDefault` / `Confidence` / `MatchMethod` become
   per-household, or stay global? "Best guess at a match" arguably stays global;
   decide deliberately rather than by default.
2. **Discover copy image** — own blob copy (leaning yes) vs. reference the original's.
3. **Joining a non-empty household** — exact merge UX (auto-merge vs. confirm
   screen vs. block until the user empties/exports their old household).
4. **Household roles** — are all members equal (current assumption), or does the
   creator get extra rights (remove members, revoke invites)?
5. **Re-sharing invite links** — can any member generate invites, or only the creator?

(Resolved: identity approach, signup model, one-household-per-user, ownership
model, seed owner, DB engine timing.)

## Suggested Phasing

Delivery: one PR per phase. Until Phase 0 lands, CI auto-deploys every PR push to
prod, so Phase 0 comes first. After it, roll each phase out to prod only after it
has been exercised on staging: put the seed config on the VM
(`/etc/recipehelper/appsettings.Production.json` / Key Vault) before the prod
deploy, and don't use `deploy/deploy.sh` (it overwrites that file with the local one).

### Phase 0 — Staging environment (prerequisite)

Today every PR push auto-deploys to prod, and local runs only work via the
Production profile against the real Azure SQL DB and blob container. Neither is
safe for an auth wall or a backfill migration, so stand up staging first:

1. **Second instance on the same VM:** separate systemd unit (e.g.
   `recipehelper-staging`) on its own port, own app root, behind a second nginx
   vhost with its own DuckDNS name (e.g. `sutherlins-staging.duckdns.org`).
   No extra VM cost.
2. **Separate database:** an Azure SQL database restored from a copy of prod
   (serverless/free tier keeps cost near zero). Lets the Phase C backfill be
   rehearsed on real-shaped data before touching prod.
3. **Separate blob container** (e.g. `recipe-images-staging`) so test uploads and
   deletes can't affect real images.
4. **Separate config:** `/etc/recipehelper-staging/appsettings.Production.json`
   with its own connection string and seed admin; no shared secrets on disk.
5. **Kroger:** register a staging OAuth redirect URI in the Kroger developer
   portal (redirect URIs are per app), or test Kroger flows only on prod.
6. **CI change:** PR branches deploy to staging automatically; prod deploys only
   from `main` or a manual run. This also removes the "auth wall goes live on
   push" risk noted above. Update `deploy/remote/README.md` and `CLAUDE.md`.
7. **Verify:** deploy a trivial PR to staging, confirm prod is untouched (version
   stamp, DB, blobs), then refresh the staging DB from prod before each phase's
   migration rehearsal.

Cheaper stopgap for Phase A only: a throwaway local SQL Server (Docker/LocalDB)
covers login, cookies and the auth wall, but not nginx, HTTPS cookies, the
service worker or Kroger — staging is still needed before Phase C.

### Phase A — Identity + auth wall (data still global)

1. Migration `AddUsers`; `UserService` (`CreateAsync`, `ValidateCredentialsAsync`
   with `PasswordHasher<User>`).
2. `AccountController` + `Views/Account/Login.cshtml`: login/logout, claims
   (user id, display name), `Url.IsLocalUrl` check on `returnUrl`, generic error
   message, per-email+IP backoff via `IMemoryCache`. Mobile-first, ≥44px targets.
3. `Program.cs`: `AddAuthentication().AddCookie` (HttpOnly, Secure, SameSite=Lax,
   sliding ~30 days, `LoginPath=/Account/Login`); `FallbackPolicy =
   RequireAuthenticatedUser`; `UseAuthentication()` before `UseAuthorization()`.
4. `[AllowAnonymous]`: `Account/Login`, `ShareController`, `HomeController.Error`,
   `AuthController.Callback`. Static files bypass auth already.
5. Seed admin on first boot (see Migration of Existing Data).
6. Service worker: don't cache `/Account/*` or login redirects; clear page caches
   on logout (shared phone).
7. JS fetches: return 401 (not a 302 to HTML) when unauthenticated; client
   redirects to login.

### Phase B — Households + invites

1. Migration `AddHouseholds` (`Household`, `User.HouseholdId`, `HouseholdInvite`).
2. Account creation always creates a solo household, unless signup carries a valid
   invite token, in which case the user joins that household.
3. Invite link `/Account/Join?token=…`: new users register through it (this is the
   invite-only signup gate); signed-in users confirm and join.
4. Joining/leaving behavior per the trade-offs above (empty household deleted,
   non-empty merged; leaving creates a fresh solo household and offers recipe copy-out).
5. UI: Settings → Household — members, "Invite someone" (generate/copy link), leave.

### Phase C — Household ownership + query scoping

1. Migration adding `HouseholdId` to `Recipe` / `MealPlan` / `ShoppingList` /
   `DraftRecipe` and `Recipe.CreatedByUserId`, backfilled to the seed household.
2. `ICurrentUser`; scope every site from the audit; consider global query filters.
3. `KrogerCustomerToken.UserId`; store preference cookie → household.
4. Household isolation tests (two households; every service method and by-id
   lookup returns only its own data) are the safety net — a missed query leaks data.

### Phase D — `IsPublic` + Discover + copy flow

Anonymous-viewable Discover, "Make public" toggle, "Copy to My Recipes".

### Independent, any time

Database engine migration, if pursued. Email invites, email verification and
password reset when outbound email exists. Rename the Kroger `auth/*` routes.

## Verification (per phase)

- Unit tests (`RecipeHelper.Tests`): password hash/verify, duplicate email, invite
  token single-use/expiry, `returnUrl` rejection; household isolation (Phase C).
- Playwright on the iPhone 14 viewport: anonymous → login redirect; share link
  works anonymously; login/logout; back button after logout shows no cached pages;
  invite-link flow with a second account joining and both editing the same meal
  plan; Kroger connect flow still completes while logged in; meal-plan autosave works.
- After deploy, `scripts/kql.sh` on `AppRequests` for unexpected 302/401/404 spikes.

## Related

- Recipe share-link feature — issue
  [#91](https://github.com/suthermj/RecipeHelper/issues/91) — public read-only link
  to a single recipe, no accounts involved. Smaller and orthogonal; can ship before
  or after without conflict.
