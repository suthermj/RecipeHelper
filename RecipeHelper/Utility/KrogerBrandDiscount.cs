namespace RecipeHelper.Utility
{
    // Kroger takes 10% off its own brands at checkout. The app never applies this --
    // Kroger prices the cart -- it only shows an estimate. Same brand list and rounding
    // as the inline copies in Cart/PreviewAddToCart.cshtml and ShoppingList views.
    public static class KrogerBrandDiscount
    {
        public const decimal Rate = 0.10m;

        private static readonly string[] Brands = { "kroger", "simple truth", "private selection", "hemisfares" };

        public static bool IsKrogerBrand(string? brand) =>
            brand != null && Brands.Any(b => brand.Contains(b, StringComparison.OrdinalIgnoreCase));

        // The price the shopper should expect to pay per unit: the sale price when on
        // sale, else the regular price, less 10% for Kroger brands (stacked on the sale).
        public static decimal EstimatedPrice(string? brand, decimal regularPrice, decimal promoPrice)
        {
            var basePrice = promoPrice > 0 && promoPrice < regularPrice ? promoPrice : regularPrice;
            return IsKrogerBrand(brand) ? Math.Round(basePrice * (1 - Rate), 2) : basePrice;
        }
    }
}
