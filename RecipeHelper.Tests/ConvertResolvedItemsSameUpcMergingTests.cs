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
    // Reproduces a real reported over-order: 4 differently-named garlic ingredient rows
    // ("garlic", "minced garlic", etc., across different recipes) all mapped to the same
    // Kroger UPC (Spice World Minced Garlic 8 oz), each needing only a few teaspoons.
    // ConvertIngredientsToCartItems used to convert each row to a pack quantity
    // independently -- every small amount rounded up to "1 jar" on its own -- and only
    // THEN grouped by UPC and summed the already-rounded quantities (1+1+1+1 = 4 jars)
    // instead of summing the actual combined need (well under 1 jar) before rounding
    // once. ConvertResolvedItemsToCartItems groups by UPC first, so this should now
    // produce exactly one row with quantity 1.
    public class ConvertResolvedItemsSameUpcMergingTests
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

        private static KrogerProductDto GarlicJar() => new KrogerProductDto
        {
            upc = "0001111041700",
            name = "Spice World Minced Garlic 8 oz",
            brand = "Spice World",
            size = "8 oz",
            soldBy = "UNIT",
            categories = new List<string> { "Produce" },
            stockLevel = "HIGH",
        };

        [Fact]
        public void FourSmallAmounts_SameUpc_ProduceOneRow_NotFourJars()
        {
            var service = BuildService();
            var product = GarlicJar();

            // Matches the screenshot: "4 Teaspoons + 3 Teaspoons + 4 Teaspoons + 2 Tablespoons"
            var resolved = new List<(CartItemVM Item, KrogerProductDto Product)>
            {
                (new CartItemVM { Name = "garlic", Upc = product.upc, Quantity = 4, Measurement = "Teaspoons", Include = true }, product),
                (new CartItemVM { Name = "minced garlic", Upc = product.upc, Quantity = 3, Measurement = "Teaspoons", Include = true }, product),
                (new CartItemVM { Name = "garlic paste", Upc = product.upc, Quantity = 4, Measurement = "Teaspoons", Include = true }, product),
                (new CartItemVM { Name = "jarred garlic", Upc = product.upc, Quantity = 2, Measurement = "Tablespoons", Include = true }, product),
            };

            var result = service.ConvertResolvedItemsToCartItems(resolved);

            // 4+3+4 tsp + 2 tbsp(=6 tsp) = 17 tsp total needed. Even with the DensityTable
            // gap for "garlic" (falls back to water density, 1.0 g/ml), 17 tsp of water is
            // ~83.8g against an 8oz (~226.8g) jar -- well under one jar.
            var row = Assert.Single(result);
            Assert.Equal(1, row.Quantity);
            Assert.Equal("4 Teaspoons + 3 Teaspoons + 4 Teaspoons + 2 Tablespoons", row.OriginalIngredient);
        }

        [Fact]
        public void LargeEnoughCombinedAmount_SameUpc_RoundsUpToMultiplePacks()
        {
            var service = BuildService();
            var product = GarlicJar();

            // Four rows that individually would each round up to 1 jar (correctly, since
            // each alone genuinely needs close to/over a full jar) should still combine
            // and round up ONCE against the true total, not four separate times.
            var resolved = new List<(CartItemVM Item, KrogerProductDto Product)>
            {
                (new CartItemVM { Name = "garlic", Upc = product.upc, Quantity = 30, Measurement = "Teaspoons", Include = true }, product),
                (new CartItemVM { Name = "minced garlic", Upc = product.upc, Quantity = 30, Measurement = "Teaspoons", Include = true }, product),
            };

            var result = service.ConvertResolvedItemsToCartItems(resolved);

            var row = Assert.Single(result);
            // 60 tsp total * 4.92892 ml/tsp * 1.0 g/ml (default density) ≈ 295.7g vs 226.8g
            // jar -- just over one jar, so this should round up to 2, not 1 and not 4.
            Assert.Equal(2, row.Quantity);
        }

        [Fact]
        public void DifferentUpcs_StayAsSeparateRows()
        {
            var service = BuildService();
            var productA = GarlicJar();
            var productB = new KrogerProductDto
            {
                upc = "0002222058800", name = "Kroger Minced Garlic 4.5 oz", brand = "Kroger",
                size = "4.5 oz", soldBy = "UNIT", categories = new List<string>(), stockLevel = "HIGH",
            };

            var resolved = new List<(CartItemVM Item, KrogerProductDto Product)>
            {
                (new CartItemVM { Name = "garlic", Upc = productA.upc, Quantity = 4, Measurement = "Teaspoons", Include = true }, productA),
                (new CartItemVM { Name = "garlic", Upc = productB.upc, Quantity = 4, Measurement = "Teaspoons", Include = true }, productB),
            };

            var result = service.ConvertResolvedItemsToCartItems(resolved);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, r => r.Upc == productA.upc);
            Assert.Contains(result, r => r.Upc == productB.upc);
        }
    }
}
