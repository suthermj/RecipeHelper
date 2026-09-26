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
    // Reproduces a reported duplicate on the Kroger cart preview: "Green Onions" showed
    // up twice, both rows reading "Needed: 0.5 Cups + 1 Unit". Two recipes mapped to the
    // same UPC, one in a volume unit and one as a count. ConvertResolvedItemsToCartItems
    // grouped them by UPC but then emitted one row per dimension bucket (volume / weight /
    // count), so the same product was listed -- and ordered -- once per bucket.
    public class ConvertResolvedItemsMixedDimensionTests
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

        private static KrogerProductDto GreenOnions() => new KrogerProductDto
        {
            upc = "0000000004068",
            name = "Green Onions",
            brand = "",
            size = "1 bunch",
            soldBy = "UNIT",
            categories = new List<string> { "Produce" },
            stockLevel = "HIGH",
        };

        private static KrogerProductDto Flour() => new KrogerProductDto
        {
            upc = "0001111087930",
            name = "Kroger All Purpose Flour",
            brand = "Kroger",
            size = "5 lb",
            soldBy = "UNIT",
            categories = new List<string> { "Baking" },
            stockLevel = "HIGH",
        };

        [Fact]
        public void VolumeAndCount_SameUpc_ProduceOneRow()
        {
            var service = BuildService();
            var product = GreenOnions();
            var resolved = new List<(CartItemVM, KrogerProductDto)>
            {
                (new CartItemVM { Upc = product.upc, Name = "Green Onions", Quantity = 0.5m, Measurement = "Cups", Include = true }, product),
                (new CartItemVM { Upc = product.upc, Name = "green onion", Quantity = 1, Measurement = "Unit", Include = true }, product),
            };

            var result = service.ConvertResolvedItemsToCartItems(resolved);

            var row = Assert.Single(result);
            Assert.Equal(1, row.Quantity);
            Assert.Equal("0.5 Cups + 1 Unit", row.OriginalIngredient);
            // Mixing a count with a volume can't be converted exactly, so it's flagged.
            Assert.False(string.IsNullOrWhiteSpace(row.ConversionNote));
        }

        [Fact]
        public void VolumeAndWeight_SameUpc_CombineIntoOneRowByWeight()
        {
            var service = BuildService();
            var product = Flour();
            // 2 cups of flour (~250 g at flour's density) + 2 kg -> ~2.25 kg, just under
            // one 5 lb (2268 g) bag. Separate rows would have ordered 2 bags.
            var resolved = new List<(CartItemVM, KrogerProductDto)>
            {
                (new CartItemVM { Upc = product.upc, Name = "flour", Quantity = 2, Measurement = "Cups", Include = true }, product),
                (new CartItemVM { Upc = product.upc, Name = "all purpose flour", Quantity = 2000, Measurement = "Grams", Include = true }, product),
            };

            var result = service.ConvertResolvedItemsToCartItems(resolved);

            var row = Assert.Single(result);
            Assert.Equal(1, row.Quantity);
        }
    }
}
