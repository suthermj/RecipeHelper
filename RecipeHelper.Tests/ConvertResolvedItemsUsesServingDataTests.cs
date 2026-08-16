using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RecipeHelper;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Services;
using Xunit;

namespace RecipeHelper.Tests
{
    // End-to-end version of KrogerPackInfoServingDataTests: with real serving data
    // present, the garlic scenario from the over-order fix converts via a direct
    // same-dimension division (tsp needed ÷ tsp per jar) instead of the cross-dimension
    // density guess -- no ConversionNote at all, since nothing was assumed.
    public class ConvertResolvedItemsUsesServingDataTests
    {
        private sealed class ThrowingHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) =>
                throw new InvalidOperationException("ConvertResolvedItemsToCartItems is pure -- it must not make HTTP calls.");
        }

        private static KrogerService BuildService()
        {
            var dbOptions = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new DatabaseContext(dbOptions);
            var config = new ConfigurationBuilder().Build();
            var authService = new KrogerAuthService(
                db, new ThrowingHttpClientFactory(), config,
                new Microsoft.AspNetCore.Http.HttpContextAccessor(), NullLogger<KrogerAuthService>.Instance);

            return new KrogerService(
                new ThrowingHttpClientFactory(), config, NullLogger<KrogerService>.Instance,
                authService, new MemoryCache(new MemoryCacheOptions()),
                new Microsoft.AspNetCore.Http.HttpContextAccessor());
        }

        [Fact]
        public void GarlicJar_WithServingData_ConvertsCleanly_NoConversionNote()
        {
            var service = BuildService();
            // Real response shape for UPC 0007096900009: servingSize 1 tsp x 45 servings.
            var product = new KrogerProductDto
            {
                upc = "0007096900009", name = "Spice World Minced Garlic 8 oz", brand = "Spice World",
                size = "8 oz", soldBy = "UNIT", categories = new List<string> { "Baking Goods" }, stockLevel = "HIGH",
                servingSizeQty = 1, servingSizeUnitAbbreviation = "tsp", servingsPerPackage = 45,
            };

            var resolved = new List<(CartItemVM Item, KrogerProductDto Product)>
            {
                (new CartItemVM { Name = "garlic", Upc = product.upc, Quantity = 4, Measurement = "Teaspoons", Include = true }, product),
                (new CartItemVM { Name = "minced garlic", Upc = product.upc, Quantity = 3, Measurement = "Teaspoons", Include = true }, product),
                (new CartItemVM { Name = "garlic paste", Upc = product.upc, Quantity = 4, Measurement = "Teaspoons", Include = true }, product),
                (new CartItemVM { Name = "jarred garlic", Upc = product.upc, Quantity = 2, Measurement = "Tablespoons", Include = true }, product),
            };

            var result = service.ConvertResolvedItemsToCartItems(resolved);

            var row = Assert.Single(result);
            // 17 tsp needed / 45 tsp per jar = 0.38 -> ceil 1, same numeric result as the
            // density-fallback path, but now via a direct division with no guess -- no
            // ConversionNote at all (would previously read "Cross-dimension: used
            // default density (water) — review quantity"), so this now surfaces in
            // "Correctly mapped" instead of "Needs review" on the cart preview.
            Assert.Equal(1, row.Quantity);
            Assert.Null(row.ConversionNote);
        }

        [Fact]
        public void BrothCarton_WithServingData_ConvertsCleanly_NoConversionNote()
        {
            var service = BuildService();
            // Real response shape for UPC 0001111004969: servingSize 240 ml x 4 servings.
            var product = new KrogerProductDto
            {
                upc = "0001111004969", name = "Kroger® 99% Fat Free Chicken Broth", brand = "Kroger",
                size = "32 oz", soldBy = "UNIT", categories = new List<string> { "Soup" }, stockLevel = "HIGH",
                servingSizeQty = 240, servingSizeUnitAbbreviation = "ml", servingsPerPackage = 4,
            };

            var resolved = new List<(CartItemVM Item, KrogerProductDto Product)>
            {
                (new CartItemVM { Name = "chicken broth", Upc = product.upc, Quantity = 0.5m, Measurement = "Cups", Include = true }, product),
                (new CartItemVM { Name = "broth", Upc = product.upc, Quantity = 1.75m, Measurement = "Cups", Include = true }, product),
                (new CartItemVM { Name = "stock", Upc = product.upc, Quantity = 8, Measurement = "Cups", Include = true }, product),
            };

            var result = service.ConvertResolvedItemsToCartItems(resolved);

            var row = Assert.Single(result);
            Assert.Null(row.ConversionNote);
            // 0.5 + 1.75 + 8 = 10.25 Cups needed = 2425.7 ml, / 960 ml per carton (240ml x
            // 4 servings) = 2.53 -> ceil 3, matching the real reported screenshot's Qty 3
            // for this exact ingredient combination -- confirms serving data reproduces
            // the same real-world answer while dropping the density-guess warning.
            Assert.Equal(3, row.Quantity);
        }
    }
}
