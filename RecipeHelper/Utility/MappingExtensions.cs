using RecipeHelper.Models.Kroger;

namespace RecipeHelper.Utility
{
    public static class MappingExtensions
    {
        // Example: Kroger cart item DTO -> DetailedCartItem
        public static DetailedCartItem ToDetailedCartItem(this Product source, int quantity = 0)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            // Adjust property names based on your actual KrogerCartItem model
            return new DetailedCartItem
            {
                Name = source.description,
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
        public static List<DetailedCartItem> ToDetailedCartItems(this IEnumerable<Product> source)
        {
            if (source == null) return new List<DetailedCartItem>();
            return source.Select(ToDetailedCartItem).ToList();
        }

        // Example: Aggregated ingredient -> CartItemVM for AddToCart
        // (Assumes you have something like an AggregatedIngredient model)
        /*public static CartItemVM ToCartItemVm(this AggregatedIngredient ingredient, int krogerUnitsNeeded, string modality = "PICKUP")
        {
            if (ingredient == null) throw new ArgumentNullException(nameof(ingredient));

            return new CartItemVM
            {
                Upc = ingredient.Upc,
                Quantity = krogerUnitsNeeded,
                Measurement = ingredient.Measurement,  // or normalized measurement string if you have it
                Modality = modality,
                Include = true
            };
        }

        // Example collection mapping for AddToCart items
        public static List<CartItemVM> ToCartItemVms(this IEnumerable<AggregatedIngredient> ingredients, Func<AggregatedIngredient, int> unitCalculator, string modality = "PICKUP")
        {
            if (ingredients == null) return new List<CartItemVM>();
            if (unitCalculator == null) throw new ArgumentNullException(nameof(unitCalculator));

            return ingredients
                .Select(i => i.ToCartItemVm(unitCalculator(i), modality))
                .ToList();
        }*/
    }
}
