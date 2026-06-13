# RecipeHelper Agent Notes

This repository is an ASP.NET Core MVC app for managing recipes, ingredients, shopping lists, dinner planning, and Kroger-backed product selection.

## Product Direction

- The app should feel like an iOS app, even though it is implemented as a web app/PWA.
- Prefer mobile-first interaction patterns, clear hierarchy, tight spacing discipline, and polished touch-friendly controls.
- When suggesting or implementing UI changes, optimize for an app-like experience rather than a traditional desktop website feel.
- The current UI/UX is considered underpowered and is an active area of improvement.
- Claude is currently applying UI/UX changes in parallel. Expect in-flight frontend edits and review them carefully instead of overwriting them.
- Once those changes land, the next useful Codex task is likely a design/UX review pass rather than a broad rewrite.

## Stack

- .NET 8 MVC app
- Entity Framework Core with SQL Server
- Razor views
- Tailwind CSS build via npm
- OpenAI SDK for ingredient parsing/canonicalization
- Azure Blob Storage for recipe images
- OpenTelemetry OTLP export

## Project Layout

- `Program.cs`: DI setup, middleware, session, static file caching, OpenTelemetry, route map
- `DatabaseContext.cs`: EF Core model and relationships
- `Controllers/`: MVC endpoints and page flow
- `Services/`: application/business logic and external API integrations
- `Models/`: EF entities plus API/request models
- `Views/`: Razor UI
- `wwwroot/`: static assets
- `Migrations/`: EF Core migrations

## Main Runtime Behavior

- Default route is `{controller=Recipe}/{action=Recipe}/{id?}`.
- Sessions are enabled and used by the app.
- Data protection keys are persisted to `/var/lib/recipehelper/keys` in `Program.cs`. This path is Linux-oriented and matters for deployed environments.
- Static asset caching is configured in `Program.cs`; `sw.js` is `no-store`, versioned CSS/JS are cached long-term.

## Configuration

App settings expect these sections/values:

- `ConnectionString`
- `StorageSettings`
- `Kroger`
- `Oauth`
- `Spoonacular`
- `OpenAI`
- `OpenTelemetry`

Development launch profiles are in `Properties/launchSettings.json`.

Local URLs:

- HTTP: `http://localhost:5074`
- HTTPS: `https://localhost:7127`

Treat API keys, OAuth secrets, storage connection strings, and OTLP auth headers as secrets. Do not duplicate or expose them in commits.

## Build and Run

- Restore/build: `dotnet build`
- Run app: `dotnet run`
- Build CSS: `npm run css:build`

`RecipeHelper.csproj` runs the Tailwind build before `Build`, so Node dependencies need to be installed for normal builds to succeed.

## Data and Domain Notes

- Recipes store ordered ingredients via `RecipeIngredient.SortOrder`.
- Recipe instructions are serialized JSON strings in the database.
- `Ingredient` and `KrogerProduct` are linked through `IngredientKrogerProduct`.
- `RecipeIngredient.SelectedKrogerUpc` is optional.
- `Measurement` records are part of the core recipe editing flow.
- Meal-planning entities are `MealPlan` and `MealPlanEntry`.

## Existing Patterns

- Keep controller actions thin; put business logic in `Services/`.
- Prefer EF Core query projections in controllers for view models.
- Reuse existing service boundaries instead of introducing new cross-cutting helpers.
- Preserve ingredient ordering and section data when editing recipe flows.
- Follow current Razor/MVC structure instead of adding SPA-style patterns.
- Treat the PWA shell as a delivery mechanism, but aim for native-app quality in flows, layout, and interaction details.

## Editing Guidance

- Check for uncommitted user changes before editing; this repo is often worked in with a dirty tree.
- Keep changes scoped. Do not reformat unrelated Razor/CSS files.
- Avoid changing configuration defaults unless the task requires it.
- When touching build or frontend assets, keep Tailwind output assumptions in mind.

## Validation

For most changes, validate with the smallest useful command first:

- `dotnet build`
- targeted `dotnet run` check when behavior is runtime/UI-related

There is no separate test project in this repository at the moment, so build and focused manual verification are the primary checks unless tests are added later.
