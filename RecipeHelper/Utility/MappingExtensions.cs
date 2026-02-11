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

            // Adjust property names based on your actual KrogerCartItem model
            return new KrogerProductDto
            {
                ProductId = source.productId,
                name = source.description,
                upc = source.upc,
                regularPrice = source.items.FirstOrDefault()?.price?.regular ?? 0,
                promoPrice = source.items.FirstOrDefault()?.price?.promo ?? 0,
                stockLevel = source.items.FirstOrDefault()?.inventory?.stockLevel ?? "N/A",
                brand = source.brand,
                aisleLocation = source.aisleLocations.FirstOrDefault()?.number ?? "N/A",
                categories = source.categories?.ToList() ?? new List<string>(),
                soldBy = source.items.FirstOrDefault()?.soldBy ?? "N/A", // Assuming the first item is representative
                size = source.items?.FirstOrDefault()?.size ?? "N/A",
                unitOfMeasure = source.nutritionInformation?.FirstOrDefault()?.servingSize?.unitOfMeasure?.name ?? null,
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
                Ingredients = vm.Ingredients.Select(i => new PreviewImportedRecipeIngredient
                {
                    Name = i.Name ?? "",
                    CleanName = i.CleanName ?? "",
                    Amount = i.Amount ?? 0m,
                    Unit = i.Unit
                }).ToList()
            };
        }

        public static MappedImportedRecipeVM ToVm(this ImportPreview dto)
        {
            return new MappedImportedRecipeVM
            {
                Title = dto.Title,
                Image = dto.Image,
                Ingredients = dto.Ingredients.Select(x => new IngredientPreviewVM
                {
                    Name = x.Name,
                    Amount = x.Amount,
                    Unit = x.Unit,
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
                }).ToList()
            }; ;
        }

        public static ImportRecipeRequest ToRequest(this MappedImportedRecipeVM vm)
        {
            return new ImportRecipeRequest
            {
                Title = (vm.Title ?? "").Trim(),
                Image = vm.Image,
                Ingredients = vm.Ingredients.Select(i => new ImportedIngredient
                {
                    Name = i.Name,
                    CanonicalName = i.CanonicalName,
                    IngredientId = i.IngredientId,
                    Amount = i.Amount ?? 0m,
                    Unit = i.Unit,
                    Include = i.Include,
                    Upc = i.SelectedUpc,
                    SelectedSource = i.SelectedSource
                }).ToList()
            };
        }

        public static EditRecipeRequest ToRequest(this EditRecipeVM vm)
        {
            return new EditRecipeRequest
            {
                Id = vm.RecipeId,
                Title = (vm.Title ?? "").Trim(),
                ImageFile = vm.ImageFile,
                Ingredients = vm.Ingredients.Select(i => new EditRecipeIngredientDto
                {
                    Id = i.Id,
                    DisplayName = i.DisplayName ?? "",
                    Quantity = i.Quantity,
                    MeasurementId = i.MeasurementId,
                    IngredientId = i.IngredientId,
                    SelectedKrogerUpc = i.SelectedKrogerUpc,
                }).ToList()
            };
        }

        public static CreateRecipeRequest ToRequest(this CreateRecipeVM2 vm)
        {
            return new CreateRecipeRequest
            {
                Title = (vm.Title ?? "").Trim(),
                ImageFile = vm.ImageFile,
                Ingredients = vm.Ingredients.Select(i => new CreateRecipeIngredientDto
                {
                    DisplayName = i.DisplayName ?? "",
                    Quantity = i.Quantity, //?? 0m,
                    MeasurementId = i.MeasurementId,
                    SelectedKrogerUpc = i.SelectedKrogerUpc,
                }).ToList()
            };
        }

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
                }).ToList()
            };
        }
    }
}
