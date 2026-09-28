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
    // A counted ingredient ("8 tortillas") against a multi-piece package must divide by
    // pieces-per-pack, not order one package per piece. Product shapes below are real
    // Kroger /products/{upc} responses (Mariemont store, 2026-09-28).
    public class CountIngredientPiecesPerPackTests
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

        private static DetailedCartItem Convert(KrogerProductDto product, decimal count)
        {
            var resolved = new List<(CartItemVM Item, KrogerProductDto Product)>
            {
                (new CartItemVM { Name = "tortillas", Upc = product.upc, Quantity = count, Measurement = "Unit", Include = true }, product),
            };
            return Assert.Single(BuildService().ConvertResolvedItemsToCartItems(resolved));
        }

        private static KrogerProductDto Tortillas(string upc, string name, string size, decimal? servingsPerPackage) => new()
        {
            upc = upc, name = name, brand = "Kroger", size = size, soldBy = "UNIT",
            categories = new List<string> { "Bakery" }, stockLevel = "HIGH",
            servingSizeQty = 71, servingSizeUnitAbbreviation = "g", servingsPerPackage = servingsPerPackage,
        };

        [Theory]
        [InlineData(8, 1)]
        [InlineData(12, 2)]
        public void CompositeSize_WithServingData_DividesByCountEach(decimal needed, int expectedPacks)
        {
            // UPC 0001111005266 -- the reported bug: size "8 ct / 20 oz" plus 71 g x 8 serving data.
            var product = Tortillas("0001111005266", "Kroger® Burrito Size Flour Tortillas", "8 ct / 20 oz", 8);

            var row = Convert(product, needed);

            Assert.Equal(expectedPacks, row.Quantity);
            Assert.Null(row.ConversionNote);
        }

        [Fact]
        public void SimpleCountSize_WithServingData_DividesByCount()
        {
            // UPC 0002733110111: size "8 ct" plus serving data.
            var product = Tortillas("0002733110111", "La Banderita Burrito Grande Extra Large Flour Tortilla", "8 ct", 8);

            var row = Convert(product, 8);

            Assert.Equal(1, row.Quantity);
            Assert.Null(row.ConversionNote);
        }

        [Fact]
        public void WeightOnlySize_CountInName_DividesByNameCount_Estimated()
        {
            // UPC 0007520230314: size "29.35 oz", no serving data, "8 ct" only in the name.
            var product = new KrogerProductDto
            {
                upc = "0007520230314", name = "Sonora Style Burrito 12\" 8 ct", brand = "Sonora", size = "29.35 oz",
                soldBy = "UNIT", categories = new List<string> { "Bakery" }, stockLevel = "HIGH",
            };

            Assert.Equal(1, Convert(product, 8).Quantity);
            var row = Convert(product, 10);
            Assert.Equal(2, row.Quantity);
            Assert.NotNull(row.ConversionNote);
        }

        [Fact]
        public void WeightOnlySize_ServingsPerPackageOnly_DividesByServings_Estimated()
        {
            // UPC 0002733110112: size "20.5 oz", 97 g x 6 servings, no count in the name.
            var product = Tortillas("0002733110112", "La Banderita Super Burrito Flour Tortillas", "20.5 oz", 6);
            product.servingSizeQty = 97;

            var row = Convert(product, 8);

            Assert.Equal(2, row.Quantity);
            Assert.NotNull(row.ConversionNote);
        }

        [Fact]
        public void NoCountInfoAnywhere_FallsBackToRawCount()
        {
            // UPC 0004494600015 shape minus serving count: nothing tells us pieces per pack.
            var product = Tortillas("0004494600015", "TortillaLand® Flour Tortillas Burrito Size", "20.8 oz", null);

            var row = Convert(product, 3);

            Assert.Equal(3, row.Quantity);
            Assert.NotNull(row.ConversionNote);
        }
    }
}
