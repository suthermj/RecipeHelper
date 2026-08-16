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
    // Reproduces a real over-order found live: "Sleeved Celery" (a "1 ct" product) needed
    // for "1.5 Cups" showed Qty 72; "Green Onions" (also "1 ct") needed for "10
    // Tablespoons" showed Qty 30. Both fall through every real conversion branch (a
    // Volume ingredient against a Unit-dimension pack matches neither AreSameDimension
    // nor IsCrossDimension) into the BRANCH 6 fallback, which used to
    // `Math.Ceiling(item.Quantity)` directly. After ConvertResolvedItemsToCartItems
    // started bucketing same-UPC amounts into base units (teaspoons/grams) before calling
    // ComputeCartQuantity -- see the garlic over-order fix -- that raw quantity became a
    // base-unit amount even for a single, unmerged ingredient row: "1.5 Cups" is 72 base
    // teaspoons, "10 Tablespoons" is 30 base teaspoons, matching the reported bug exactly.
    // The fallback should never fabricate a scaled guess; it defaults to 1 and says so.
    public class ComputeCartQuantityFallbackScaleTests
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

        private static KrogerProductDto UnitProduct(string upc, string name) => new KrogerProductDto
        {
            upc = upc, name = name, brand = "Test Brand", size = "1 ct", soldBy = "UNIT",
            categories = new List<string>(), stockLevel = "HIGH",
        };

        [Fact]
        public void VolumeIngredient_AgainstUnitDimensionPack_FallsBackToOne_NotScaledBaseUnits()
        {
            var service = BuildService();
            var product = UnitProduct("0001111041234", "Sleeved Celery");

            var resolved = new List<(CartItemVM Item, KrogerProductDto Product)>
            {
                (new CartItemVM { Name = "celery", Upc = product.upc, Quantity = 1.5m, Measurement = "Cups", Include = true }, product),
            };

            var result = service.ConvertResolvedItemsToCartItems(resolved);

            var row = Assert.Single(result);
            // 1.5 Cups == 72 base teaspoons -- must NOT leak through as Qty 72.
            Assert.Equal(1, row.Quantity);
            Assert.Contains("defaulted to 1", row.ConversionNote);
        }

        [Fact]
        public void VolumeIngredient_TablespoonsAgainstUnitDimensionPack_FallsBackToOne_NotThirty()
        {
            var service = BuildService();
            var product = UnitProduct("0002222055678", "Green Onions");

            var resolved = new List<(CartItemVM Item, KrogerProductDto Product)>
            {
                (new CartItemVM { Name = "green onions", Upc = product.upc, Quantity = 10, Measurement = "Tablespoons", Include = true }, product),
            };

            var result = service.ConvertResolvedItemsToCartItems(resolved);

            var row = Assert.Single(result);
            // 10 Tablespoons == 30 base teaspoons -- must NOT leak through as Qty 30.
            Assert.Equal(1, row.Quantity);
        }

        [Fact]
        public void UnparseableSize_VolumeIngredient_FallsBackToOne()
        {
            var service = BuildService();
            var product = new KrogerProductDto
            {
                upc = "0003333066789", name = "Mystery Sauce", brand = "Test Brand", size = "Varies",
                soldBy = "UNIT", categories = new List<string>(), stockLevel = "HIGH",
            };

            var resolved = new List<(CartItemVM Item, KrogerProductDto Product)>
            {
                (new CartItemVM { Name = "sauce", Upc = product.upc, Quantity = 3, Measurement = "Cups", Include = true }, product),
            };

            var result = service.ConvertResolvedItemsToCartItems(resolved);

            var row = Assert.Single(result);
            Assert.Equal(1, row.Quantity);
            Assert.Contains("defaulted to 1", row.ConversionNote);
        }

        [Fact]
        public void UnparseableSize_CountIngredient_StillUsesRawCountAsBefore()
        {
            // Count-dimension ingredients are never bucketed into scaled base units (see
            // ConvertResolvedItemsToCartItems -- the Count bucket sums raw item.Quantity
            // directly), so this fallback path's original "use it as-is" behavior is
            // still reasonable here and shouldn't be defaulted away.
            var service = BuildService();
            var product = new KrogerProductDto
            {
                upc = "0004444077890", name = "Mystery Cans", brand = "Test Brand", size = "Varies",
                soldBy = "UNIT", categories = new List<string>(), stockLevel = "HIGH",
            };

            var resolved = new List<(CartItemVM Item, KrogerProductDto Product)>
            {
                (new CartItemVM { Name = "cans", Upc = product.upc, Quantity = 3, Measurement = "Unit", Include = true }, product),
            };

            var result = service.ConvertResolvedItemsToCartItems(resolved);

            var row = Assert.Single(result);
            Assert.Equal(3, row.Quantity);
        }
    }
}
