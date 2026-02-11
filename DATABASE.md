# Database Schema

SQL Server database managed via Entity Framework Core.

## Entity Relationship Diagram

```
Recipe (1) ──────── (N) RecipeIngredient (N) ──────── (1) Ingredient
                              |    |                        |
                              |    |                        | (N)
                         (0..1)    (0..1)                   |
                              |    |              IngredientKrogerProduct
                              |    |                        |
                       Measurement |                        | (N)
                                   |                        |
                             KrogerProduct ─────────────────┘
                               (Upc PK)

KrogerCustomerToken (standalone — Kroger OAuth storage)
DraftRecipe (standalone — references Recipe.Id optionally)
Product (legacy — appears unused)
```

## Tables

### Recipe
Stores top-level recipe metadata.

| Column   | Type   | Constraints  | Notes              |
|----------|--------|--------------|--------------------|
| Id       | int    | PK, Identity |                    |
| Name     | string | Required     | Recipe title       |
| ImageUri | string | Nullable     | Blob storage URL   |

**Relationships:** One Recipe → many RecipeIngredients

---

### RecipeIngredient
A single ingredient line within a recipe. Central join table linking recipes to canonical ingredients, measurements, and optionally a specific Kroger product for shopping.

| Column            | Type          | Constraints         | Notes                          |
|-------------------|---------------|---------------------|--------------------------------|
| Id                | int           | PK, Identity        |                                |
| RecipeId          | int           | FK → Recipe.Id      |                                |
| IngredientId      | int           | FK → Ingredient.Id  | Canonical ingredient           |
| DisplayName       | string        | Required            | User-facing name (e.g. "diced yellow onion") |
| Quantity          | decimal(10,2) | Required            |                                |
| MeasurementId     | int?          | FK → Measurement.Id | Null = "count" / unitless      |
| SelectedKrogerUpc | string?       | FK → KrogerProduct.Upc | Chosen product for shopping |

**Relationships:**
- Many-to-One → Recipe
- Many-to-One → Ingredient
- Many-to-One → Measurement (optional)
- Many-to-One → KrogerProduct (optional, FK on Upc via fluent API)

---

### Ingredient
Canonical ingredient identity. Multiple recipe lines like "diced onion" and "sliced onion" can map to the same canonical "yellow onion" ingredient.

| Column             | Type   | Constraints  | Notes                          |
|--------------------|--------|--------------|--------------------------------|
| Id                 | int    | PK, Identity |                                |
| CanonicalName      | string | Required     | Normalized name for matching   |
| DefaultDisplayName | string | Nullable     | Suggested display label        |

**Relationships:**
- One Ingredient → many RecipeIngredients
- One Ingredient → many IngredientKrogerProducts

---

### KrogerProduct
Cached Kroger product data, keyed by UPC.

| Column | Type    | Constraints | Notes              |
|--------|---------|-------------|--------------------|
| Upc    | string  | PK          | Universal Product Code |
| Name   | string  | Required    | Product name       |
| Price  | decimal | Required    | Current price      |

**Relationships:**
- One KrogerProduct → many IngredientKrogerProducts
- One KrogerProduct → many RecipeIngredients (via SelectedKrogerUpc)

---

### IngredientKrogerProduct
Bridge table linking canonical ingredients to Kroger products (many-to-many). Tracks how the mapping was established and which is the default for shopping.

| Column       | Type      | Constraints                      | Notes                                         |
|--------------|-----------|----------------------------------|-----------------------------------------------|
| IngredientId | int       | Composite PK, FK → Ingredient.Id |                                               |
| Upc          | string    | Composite PK, FK → KrogerProduct.Upc |                                           |
| IsDefault    | bool      | Required                         | Default product used for cart                 |
| Confidence   | decimal?  | Nullable                         | Match confidence score                        |
| MatchMethod  | string?   | Nullable                         | "Exact", "Fuzzy", "UserSelected", "LLM"      |
| CreatedUtc   | DateTime  | Required                         | When the mapping was created                  |

**Composite PK:** (IngredientId, Upc) — configured via fluent API.

---

### Measurement
Lookup table for units of measure.

| Column      | Type   | Constraints  | Notes                                    |
|-------------|--------|--------------|------------------------------------------|
| Id          | int    | PK, Identity |                                          |
| Name        | string | Required     | "Cups", "Teaspoons", "Tablespoons", "Ounces", "Pounds", "Grams", "Unit" |
| MeasureType | string | Required     | "volume" or "weight"                     |

---

### KrogerCustomerToken
Stores Kroger OAuth2 credentials for authenticated API calls (cart operations).

| Column                  | Type           | Constraints  | Notes                    |
|-------------------------|----------------|--------------|--------------------------|
| Id                      | int            | PK, Identity |                          |
| KrogerProfileId         | string         | Required     | Kroger user profile ID   |
| AccessToken             | string         | Required     | OAuth2 access token      |
| RefreshToken            | string         | Required     | OAuth2 refresh token     |
| AccessTokenExpiresAtUtc | DateTimeOffset | Required     | Token expiration         |

---

### DraftRecipe
Holds in-progress recipe edits before publishing. Optionally references an existing published recipe.

| Column            | Type   | Constraints               | Notes                   |
|-------------------|--------|---------------------------|-------------------------|
| Id                | int    | PK, Identity              |                         |
| Name              | string | Required                  | Draft recipe title      |
| ImageUri          | string | Nullable                  | Blob storage URL        |
| PublishedRecipeId | int?   | FK → Recipe.Id (nullable) | Source recipe if editing |

---

### Product (legacy)
Appears to be an older product table, largely unused in current flows.

| Column | Type    | Constraints  | Notes |
|--------|---------|--------------|-------|
| Id     | int     | PK, Identity |       |
| Name   | string  | Required     |       |
| Upc    | string  | Required     |       |
| Price  | decimal | Required     |       |
