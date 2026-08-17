# Multi-User Architecture — Future State

**Status:** Planning only. Nothing here is implemented. This document exists so the
architecture and data flows can be reviewed thoroughly before any code changes —
per explicit request, this is a design reference, not a task list to execute yet.

## Goal

Transform RecipeHelper from a single global-tenant app into a real multi-user app:

- Each user has their own private Recipes, Meal Plans, Shopping Lists, and Kroger
  cart connection.
- A brand-new user's Recipe page starts blank.
- Recipes are **private by default**; an owner can explicitly mark one **Public**.
- A new **Discover** tab shows Public recipes from all users, read-only.
- From Discover, a user can **copy** a public recipe into their own recipe space to
  edit it — copying, not live-syncing.
- Separately: whether to move off Azure SQL (cost) is worth evaluating, but is not
  a prerequisite for user isolation.

## Current State (baseline, as of this writing)

- **No app-level identity.** No `[Authorize]` anywhere, no `AddAuthentication` /
  `app.UseAuthentication()` in `Program.cs`, no `Users` table. Anyone who can reach
  the URL can use the app.
- The only "auth" in the codebase is `AuthController`, which does **Kroger's**
  OAuth for cart access — it authenticates the browser to Kroger's API, not to
  RecipeHelper. It stores `KrogerCustomerToken` keyed by Kroger's own
  `KrogerProfileId` and drops a `KrogerProfileId` cookie.
- Store preference (`KrogerLocationId`) is a plain browser cookie, not linked to
  any account.
- **All data is global.** `Recipe`, `MealPlan`, `ShoppingList`, `Ingredient`,
  `KrogerProduct`, etc. have no owner column — see `DATABASE.md` for the full
  current schema. Zero query in the app filters by "whose data is this."
- Blob storage (`recipe-images` container) uses flat, unscoped filenames.

## Target Data Model

### New: Identity

- A `Users` table (either full ASP.NET Core Identity, or a lighter hand-rolled
  table — see Open Questions) with at least `Id`, login credential, and
  `DisplayName` (shown on Discover cards as recipe attribution).
- Cookie-based authentication (`app.UseAuthentication()` + `[Authorize]` on
  controllers) — consistent with this being a mobile PWA with no API/native
  clients that would want a token-based scheme.

### `Recipe` — becomes owned + visibility-flagged

Add:

| Column | Type | Notes |
|---|---|---|
| `OwnerId` | int, FK → `User.Id` | Required. Who this recipe belongs to. |
| `IsPublic` | bool, default `false` | Private unless the owner explicitly flips this. |
| `ClonedFromRecipeId` | int?, FK → `Recipe.Id` | Nullable. Set when this recipe was copied from Discover — provenance only, not required for function, but cheap to add now. |

`RecipeIngredient`, ingredient sections, and images stay child records of
`Recipe` — they inherit scope through the `RecipeId` FK, no direct ownership
column needed on them.

### `MealPlan`, `ShoppingList` — become owned

Add `OwnerId` (FK → `User.Id`) to both. `MealPlanEntry` / `ShoppingListItem`
inherit scope through their parent FK, same pattern as today.

### `KrogerCustomerToken` — link to the app user

Add `UserId` (FK → `User.Id`). Today it's keyed only by Kroger's own
`KrogerProfileId` with an implicit single-user assumption baked in; multi-user
needs an explicit link from "this app account" to "which Kroger account they
connected."

### Stays global (shared reference data, not user content)

- `Ingredient` — canonical ingredient identity, genuinely global vocabulary.
- `KrogerProduct` — Kroger's own product catalog, not user data.
- `IngredientKrogerProduct` — the mapping between the two global tables above.
- `Measurement` — lookup table.

These don't get an `OwnerId`. See Open Questions below for one nuance
(per-user product preference) that's worth a deliberate call rather than a
default.

### `DraftRecipe`, `Product` (legacy)

`DraftRecipe` should get `OwnerId` too if it's kept. `Product` is already
flagged in `DATABASE.md` as unused — a candidate for removal rather than
migration.

## Query-Scoping Audit

This is the actual bulk of the implementation work, not the schema change.
Every existing call site below needs an explicit "does this need
`WHERE OwnerId == currentUser.Id`" review, and every controller action needs
`[Authorize]`:

| File | Raw `_context.Recipes` / `MealPlans` / `ShoppingLists` call sites |
|---|---|
| `Services/ShoppingListService.cs` | 8 |
| `Services/MealPlanService.cs` | 6 |
| `Controllers/RecipeController.cs` | 5 |
| `Controllers/DinnerController.cs` | 3 |
| `Services/RecipeService.cs` | 2 |
| `Services/ImportService.cs` | 1 |

(Snapshot count — re-grep before starting, this will drift as the app grows.)

Every service method that currently has no notion of "current user" needs one
threaded through — most likely via `IHttpContextAccessor` reading the
authenticated `ClaimsPrincipal`, the same general shape already used to read
`KrogerLocationId` from cookies today.

**The one deliberate exception:** Discover-tab queries intentionally read
*across* ownership boundaries (`WHERE IsPublic == true`, no owner filter).
Isolate this into its own clearly-named service method so it doesn't get
accidentally "fixed" into full isolation later — and so it's the *only* place
in the codebase allowed to query across users.

## Discover Tab & Copy Flow

- `DiscoverController` / `Views/Discover/Index.cshtml` — cards for
  `IsPublic == true` recipes across all users. Read-only: no edit/delete
  affordances, similar shape to the read-only view already built for the
  meal-plan share link.
- Each card shows the owner's `DisplayName`.
- **"Copy to My Recipes"** action:
  - Deep-clones the `Recipe` row plus its `RecipeIngredient` rows — new
    primary keys, `OwnerId = currentUser.Id`, `IsPublic = false` on the copy
    (private until the new owner chooses otherwise), `ClonedFromRecipeId` set
    to the source recipe's id.
  - Kroger product mappings (`SelectedKrogerUpc`) carry over as-is — they're
    just references into the global `KrogerProduct` table, safe to copy
    without modification.
  - **Open question — the image:** does the copy get its own blob image, or
    keep pointing at the original owner's blob URL? Sharing the URL is
    simpler, but the copy silently breaks if the original owner later
    replaces or deletes their image. Leaning toward copying the blob at clone
    time — one extra blob-copy operation, avoids a dangling-reference failure
    mode that would only surface much later.
- Recipe visibility toggle: a "Make public" switch on the recipe edit page,
  off by default — matches "private unless the user makes it public."
- Clones are always fully independent after copying — no live-sync back to
  the original. Noting this explicitly so it isn't revisited later as "should
  updates flow through"; the request as given was copy-then-diverge.

## Migration of Existing Data

The current single dataset needs an owner on cutover day. Plan: create one
seed user (the current/primary account), and in the same migration that adds
`OwnerId` to `Recipe` / `MealPlan` / `ShoppingList`, backfill every existing
row to that seed user's id — not a two-step nullable-then-backfill-then-required
process unless a longer backfill window turns out to be needed.

## Database Engine (separate decision, its own track)

Raised because Azure SQL Database carries an ongoing cost independent of the
Hetzner VPS already being paid for either way. Not decided — a recommendation
to evaluate, and explicitly **not** a prerequisite for the multi-user work
above:

- **Don't split storage across database technologies.** The schema is
  thoroughly relational — FKs, cascading deletes, a filtered unique index on
  `MealPlan.ShareToken` — and multi-user adds *more* relational structure
  (`OwnerId` FKs everywhere), not less. Splitting across two engines means two
  systems to back up, secure, and operate, for no workload that actually needs
  it.
- **Cosmos DB is a poor fit**, not just a pricier option — it's a
  document/NoSQL store, and this schema leans on joins and cascades
  throughout. Skip it unless a genuinely document-shaped need shows up later
  (none exists today).
- **The real cost lever: self-hosted Postgres on the existing Hetzner VM.**
  The app already runs on that box; running Postgres there too drops the
  Azure SQL Database bill to $0 (the VM cost is sunk either way). EF Core's
  Npgsql provider is mature, migrations regenerate cleanly, and nothing in
  the current schema is SQL-Server-exotic (the `HasFilter` partial index on
  `ShareToken` translates fine to a Postgres partial index). Real costs of
  this move: connection auth changes from
  `Authentication=Active Directory Service Principal` to Postgres-native
  credentials (stored the same way other prod secrets already are, in
  `appsettings.Production.json`); and Azure SQL's managed backups/PITR go
  away, so backups become a self-owned `pg_dump` cron (e.g. shipping to the
  same blob storage account already in use). It's a cutover with a
  maintenance window, not a live migration.
- **Middle ground:** Azure Database for PostgreSQL (Flexible Server,
  Burstable tier) — same EF Core migration effort as self-hosting, usually
  cheaper than Azure SQL, and keeps managed backups. Worth a direct price
  comparison against the current Azure SQL tier before deciding between this
  and self-hosting.
- This can happen before, after, or independently of the multi-user work — an
  infrastructure decision, not a blocker for user isolation.

## Open Questions (resolve before implementation starts)

1. **Identity approach.** Full ASP.NET Core Identity (registration, password
   reset, email confirmation, the works) vs. a minimal hand-rolled login —
   this is likely to stay a small, invite-only user base rather than public
   self-signup, which argues for the lighter option, but worth a deliberate
   choice.
2. **Per-user Kroger product preference.** Should
   `IngredientKrogerProduct.IsDefault` / `Confidence` / `MatchMethod` become
   per-user, or stay global? Today one mapping is shared by everyone. In
   multi-user, "the app's best guess at a product match" arguably should stay
   global — it's genuinely a better or worse match regardless of who's
   asking — but this deserves a deliberate call rather than defaulting either
   way by accident.
3. **Does a Discover copy get its own image, or reference the original's?**
   (Leaning toward copying the blob — see Discover section above.)
4. **DB engine migration timing** relative to the multi-user work — bundle
   together, or keep fully separate? (Recommended: separate — orthogonal, and
   doesn't block user isolation.)
5. **Seed/backfill owner** for existing data — confirm who the "existing
   dataset becomes owned by this user" migration should target.

## Suggested Phasing

1. **Identity + auth wall only**, data still global. Cheapest possible first
   step — gets a real `User` table and `[Authorize]` scaffolding in place
   without touching the data model at all.
2. **Add `OwnerId`** to `Recipe` / `MealPlan` / `ShoppingList`, backfill
   existing data to the seed user, scope every query site from the audit
   table above.
3. **Add `IsPublic` + Discover tab + copy flow.**
4. **(Independent, any time)** database engine migration, if pursued.

## Related

- Recipe share-link feature — issue
  [#91](https://github.com/suthermj/RecipeHelper/issues/91) — public
  read-only link to a single recipe, no accounts involved. Smaller and
  orthogonal to this document; can ship before or after without conflict.
