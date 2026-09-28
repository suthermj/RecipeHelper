using System.Text.Json;
using RecipeHelper.Models;
using RecipeHelper.Models.Import;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Models.RecipeModels;
using RecipeHelper.ViewModels;

namespace RecipeHelper.Utility
{
    public static class MappingExtensions
    {
        // Example: Kroger cart item DTO -> DetailedCartItem
        public static DetailedCartItem ToDetailedCartItem(this KrogerProductDto source, int quantity = 0)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Adjust property names based on your actual KrogerCartItem model
            return new DetailedCartItem
            {
                Name = source.name,
                Upc = source.upc,
                Aisle = source.aisleLocation ?? "Other",
                RegularPrice = source.regularPrice,
                PromoPrice = source.promoPrice,
                StockLevel = source.stockLevel,
                OnSale = source.onSale,
                Brand = source.brand,
                Quantity = quantity,
                Categories = source.categories?.ToList() ?? new List<string>()
            };
        }

        // Collection version (super convenient in controllers/services)
        public static List<DetailedCartItem> ToDetailedCartItems(this IEnumerable<KrogerProductDto> source)
        {
            if (source == null) return new List<DetailedCartItem>();
            return source.Select(ToDetailedCartItem).ToList();
        }

        public static KrogerProductDto ToKrogerProduct(this KrogerProductModel source, int quantity = 0)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var nutrition = source.nutritionInformation?.FirstOrDefault();

            // Adjust property names based on your actual KrogerCartItem model
            return new KrogerProductDto
            {
                ProductId = source.productId,
                name = source.description,
                upc = source.upc,
                regularPrice = source.items?.FirstOrDefault()?.price?.regular ?? 0,
                promoPrice = source.items?.FirstOrDefault()?.price?.promo ?? 0,
                stockLevel = source.items?.FirstOrDefault()?.inventory?.stockLevel ?? "N/A",
                brand = source.brand,
                aisleLocation = source.aisleLocations?.FirstOrDefault()?.number ?? "N/A",
                aisleDescription = source.aisleLocations?.FirstOrDefault()?.description,
                categories = source.categories?.ToList() ?? new List<string>(),
                soldBy = source.items?.FirstOrDefault()?.soldBy ?? "N/A", // Assuming the first item is representative
                size = source.items?.FirstOrDefault()?.size ?? "N/A",
                unitOfMeasure = nutrition?.servingSize?.unitOfMeasure?.name ?? null,
                servingSizeQty = nutrition?.servingSize?.quantity,
                // Kroger doesn't always populate `abbreviation` (e.g. unit code "G21",
                // "Cup US", comes back with only `name` set) -- fall back to the full
                // name rather than silently losing perfectly good serving data and
                // falling back to the ambiguous size string instead.
                servingSizeUnitAbbreviation = nutrition?.servingSize?.unitOfMeasure?.abbreviation
                    ?? nutrition?.servingSize?.unitOfMeasure?.name,
                servingsPerPackage = nutrition?.servingsPerPackage?.value,
            };
        }

        // Collection version (super convenient in controllers/services)
        public static List<KrogerProductDto> ToKrogerProducts(this IEnumerable<KrogerProductModel> source)
        {
            if (source == null) return new List<KrogerProductDto>();
            return source.Select(ToKrogerProduct).ToList();
        }

        public static PreviewImportedRecipeRequest ToRequest(this PreviewImportedRecipeVM vm)
        {
            return new PreviewImportedRecipeRequest
            {
                Title = (vm.Title ?? "").Trim(),
                Image = vm.Image,
                SourceUrl = vm.SourceUrl,
                Ingredients = vm.Ingredients.Select(i => new PreviewImportedRecipeIngredient
                {
                    Name = i.Name ?? "",
                    CleanName = i.CleanName ?? "",
                    Text = string.IsNullOrWhiteSpace(i.Text) ? null : i.Text.Trim(),
                    Amount = i.Amount ?? 0m,
                    Unit = i.Unit,
                    Section = i.Section
                }).ToList()
            };
        }

        public static MappedImportedRecipeVM ToVm(this ImportPreview dto)
        {
            return new MappedImportedRecipeVM
            {
                Title = dto.Title,
                Image = dto.Image,
                SourceUrl = dto.SourceUrl,
                Ingredients = dto.Ingredients.Select(x =>
                {
                    var line = x.Text ?? ComposeIngredientLine(x.Amount, x.Unit, x.Name);
                    return new IngredientPreviewVM
                    {
                        Name = x.Name,
                        Text = line,
                        OriginalText = line,
                        Amount = x.Amount,
                        Unit = x.Unit,
                        Section = x.Section,
                        IngredientId = x.MatchedIngredientId,
                        CanonicalName = x.MatchedCanonicalName,
                        SuggestedName = x.SuggestedProductName,
                        SuggestedUpc = x.SuggestedProductUpc,
                        SelectedName = x.SuggestedProductName,
                        SelectedUpc = x.SuggestedProductUpc,
                        //SelectedSource
                        Include = true,
                        Kroger = x.Kroger is null ? null : new SuggestedKrogerProductVM
                        {
                            //Upc = x.Kroger.Upc,
                            Name = x.Kroger.Name,
                            ImageUrl = x.Kroger.ImageUrl,
                            Upc = x.Kroger.Upc
                        }
                    };
                }).ToList()
            };
        }

        // "1.5 cup flour", "2 eggs", or just "salt" when there's no set amount -- the
        // fallback line for an importer that didn't supply the original text.
        public static string ComposeIngredientLine(decimal? amount, string? unit, string? name)
        {
            name = (name ?? "").Trim();
            if (amount is not > 0) return name;
            var qty = amount.Value % 1 == 0 ? ((int)amount.Value).ToString() : amount.Value.ToString("0.##");
            var u = string.IsNullOrWhiteSpace(unit) || unit.Trim().Equals("unit", StringComparison.OrdinalIgnoreCase)
                ? "" : unit.Trim() + " ";
            return $"{qty} {u}{name}".Trim();
        }

        public static ImportRecipeRequest ToRequest(this MappedImportedRecipeVM vm)
        {
            return new ImportRecipeRequest
            {
                Title = (vm.Title ?? "").Trim(),
                Image = vm.Image,
                SourceUrl = vm.SourceUrl,
                Ingredients = vm.Ingredients.Select(i => new ImportedIngredient
                {
                    Name = i.Name,
                    CanonicalName = i.CanonicalName,
                    IngredientId = i.IngredientId,
                    Text = string.IsNullOrWhiteSpace(i.Text) ? null : i.Text.Trim(),
                    OriginalText = string.IsNullOrWhiteSpace(i.OriginalText) ? null : i.OriginalText.Trim(),
                    Amount = i.Amount ?? 0m,
                    Unit = i.Unit,
                    Section = i.Section,
                    Include = i.Include,
                    Upc = i.SelectedUpc,
                    SelectedSource = i.SelectedSource
                }).ToList(),
                Steps = vm.Steps ?? new()
            };
        }

        // ToRequest for Edit/Create moved to RecipeController (needs async ingredient parsing)

        public static ViewRecipeVM ToVM(this Recipe recipe)
        {
            return new ViewRecipeVM
            {
                RecipeName = (recipe.Name ?? "").Trim(),
                ImageUri = recipe.ImageUri,
                Ingredients = recipe.Ingredients.Select(i => new IngredientVM
                {
                    Name = i.DisplayName ?? "",
                    Quantity = i.Quantity, //?? 0m,
                    Measurement = i.Measurement.Name ?? "",
                    Upc = i.SelectedKrogerUpc,
                }).ToList(),
                Instructions = string.IsNullOrEmpty(recipe.Instructions)
                    ? new()
                    : JsonSerializer.Deserialize<List<string>>(recipe.Instructions) ?? new()
            };
        }
    }
}
