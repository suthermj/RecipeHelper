# RecipeHelper

A recipe management and meal prep app with Kroger shopping cart integration. Import recipes from the web, plan weekly meals, aggregate ingredients, and add everything to your Kroger cart in one click.

**Live at:** [sutherlinsrecipes.duckdns.org](https://sutherlinsrecipes.duckdns.org)

## Features

- **Recipe Management** — Create, edit, and organize recipes with ingredient sections and step-by-step instructions
- **Recipe Import** — Import recipes from any URL via Spoonacular API with automatic ingredient parsing
- **AI Ingredient Parsing** — OpenAI-powered extraction of quantities, units, and canonical ingredient names from raw text
- **Meal Planning** — Select up to 7 recipes for the week and automatically aggregate ingredients with smart unit conversions
- **Kroger Integration** — Search Kroger products, link ingredients to specific items (by UPC), and bulk-add to your Kroger shopping cart via OAuth
- **Unit Conversion** — Handles volume, weight, and count units with cross-dimension conversion via ingredient density tables
- **Observability** — OpenTelemetry traces, metrics, and logs exported to Grafana Cloud over OTLP/HTTP

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 8 / ASP.NET Core MVC / C# 12 |
| Frontend | Razor Views, Tailwind CSS 3, vanilla JS |
| Database | SQL Server (Azure SQL) via EF Core 9 |
| AI | OpenAI GPT-4o-mini (ingredient parsing) |
| Storage | Azure Blob Storage (recipe images) |
| APIs | Spoonacular (recipe import), Kroger (products & cart) |
| Observability | OpenTelemetry SDK → Grafana Cloud (Tempo / Mimir / Loki) |
| Hosting | Hetzner Cloud VPS (Ubuntu), nginx, Let's Encrypt SSL |

## Project Structure

```
RecipeHelper/                # repo root
├── RecipeHelper/            # the ASP.NET Core project
│   ├── Controllers/         # MVC controllers (Recipe, Import, Dinner, Cart, Auth, Product)
│   ├── Models/              # Entities and view models
│   ├── Views/               # Razor templates (responsive mobile-first design)
│   ├── Services/            # Business logic
│   ├── Utility/             # UnitConverter, DensityTable, KrogerSizeParser
│   ├── Migrations/          # EF Core migrations
│   ├── wwwroot/css/         # Tailwind source (site.css) and compiled output
│   └── Program.cs           # DI, middleware, OpenTelemetry wiring
└── deploy/                  # deploy.sh + deployment runbook
```

## Getting Started

### Prerequisites

- .NET 8 SDK
- Node.js / npm
- SQL Server instance

### Setup

```bash
git clone https://github.com/suthermj/RecipeHelper.git
cd RecipeHelper
```

Configure `RecipeHelper/appsettings.json` (or use .NET User Secrets):

```json
{
  "ConnectionString": "<SQL Server connection string>",
  "StorageSettings": {
    "connectionString": "<Azure Blob connection string>"
  },
  "Kroger": {
    "baseUri": "https://api.kroger.com/v1",
    "clientId": "<Kroger client ID>",
    "clientSecret": "<Kroger client secret>"
  },
  "Spoonacular": {
    "baseUri": "https://api.spoonacular.com",
    "apiKey": "<Spoonacular API key>"
  },
  "Oauth": {
    "AuthorizeUrl": "https://api.kroger.com/v1/connect/oauth2/authorize",
    "TokenUrl": "https://api.kroger.com/v1/connect/oauth2/token",
    "ClientId": "<client ID>",
    "ClientSecret": "<client secret>",
    "RedirectUri": "https://<your-domain>/auth/callback"
  },
  "OpenAI": {
    "ApiKey": "<OpenAI API key>"
  }
}
```

### Build & Run

```bash
cd RecipeHelper
npm install
npm run css:build
dotnet build
dotnet run
```

Open [https://localhost:7127](https://localhost:7127)

### Deploy

```bash
# From repo root — builds, uploads, and restarts the service on the VM
bash deploy/deploy.sh
```

## UI Testing

The app is designed as a mobile-first iOS PWA, so UI changes must be verified on a phone-sized viewport. [Playwright](https://playwright.dev/) is used for this with an `iPhone 14` device profile.

```bash
# From RecipeHelper/ — install once
npm install --save-dev @playwright/test
npx playwright install chromium

# Run tests (interactive visual mode recommended)
npx playwright test --ui
```

See `CLAUDE.md` for the full iOS-specific checklist (safe area, tab bar, touch targets, z-index layers, etc.).

## Key Flows

### Recipe Import
URL → Spoonacular extraction → OpenAI ingredient parsing → ingredient matching & Kroger product suggestions → save

### Meal Prep
Select 1–7 recipes → aggregate ingredients across recipes → convert and consolidate units → review shopping list → add to Kroger cart

### Unit Conversion
All units normalize to base units (teaspoons for volume, grams for weight). Cross-dimension conversion (e.g., cups of flour → grams) uses per-ingredient density tables.
