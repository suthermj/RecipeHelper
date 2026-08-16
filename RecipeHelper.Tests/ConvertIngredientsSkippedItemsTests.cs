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
    // Covers Stage 0 fix #3: ConvertIngredientsToCartItems used to `continue` past
    // unmapped/lookup-failed ingredients with nothing communicated to the caller -- the
    // item just vanished between the review page and the cart preview. It now reports
    // each skip with a reason.
    //
    // No HTTP/DB calls happen in this test: GetProductsByUpcBatch short-circuits before
    // any network or token call when its UPC list is empty (see KrogerService.cs), which
    // is exactly the case for an all-unmapped AddToCartVM. A ThrowingHttpClientFactory is
    // used anyway so the test fails loudly if that ever stops being true.
    public class ConvertIngredientsSkippedItemsTests
    {
        private sealed class ThrowingHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) =>
                throw new InvalidOperationException("No HTTP calls should happen for an all-unmapped ingredient list.");
        }

        private static KrogerService BuildService()
        {
            var dbOptions = new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new DatabaseContext(dbOptions);

            var config = new ConfigurationBuilder().Build();
            var authService = new KrogerAuthService(
                db,
                new ThrowingHttpClientFactory(),
                config,
                new Microsoft.AspNetCore.Http.HttpContextAccessor(),
                NullLogger<KrogerAuthService>.Instance);

            return new KrogerService(
                new ThrowingHttpClientFactory(),
                config,
                NullLogger<KrogerService>.Instance,
                authService,
                new MemoryCache(new MemoryCacheOptions()),
                new Microsoft.AspNetCore.Http.HttpContextAccessor());
        }

        [Fact]
        public async Task ConvertIngredientsToCartItems_NoUpcMapped_AppearsInSkippedWithReason()
        {
            var service = BuildService();
            var vm = new AddToCartVM
            {
                Items = new List<CartItemVM>
                {
                    new CartItemVM { Name = "Ground Cinnamon", Upc = "", Quantity = 2, Measurement = "Teaspoons", Include = true }
                }
            };

            var result = await service.ConvertIngredientsToCartItems(vm);

            Assert.Empty(result.Items);
            var skip = Assert.Single(result.Skipped);
            Assert.Equal("Ground Cinnamon", skip.Name);
            Assert.Equal("No product mapped", skip.Reason);
            Assert.Equal(2, skip.Quantity);
        }

        [Fact]
        public async Task ConvertIngredientsToCartItems_MultipleUnmapped_AllSkippedWithNames()
        {
            var service = BuildService();
            var vm = new AddToCartVM
            {
                Items = new List<CartItemVM>
                {
                    new CartItemVM { Name = "Salt", Upc = "", Quantity = 1, Measurement = "Teaspoons", Include = true },
                    new CartItemVM { Name = "Pepper", Upc = "", Quantity = 1, Measurement = "Teaspoons", Include = true }
                }
            };

            var result = await service.ConvertIngredientsToCartItems(vm);

            Assert.Empty(result.Items);
            Assert.Equal(2, result.Skipped.Count);
            Assert.Contains(result.Skipped, s => s.Name == "Salt");
            Assert.Contains(result.Skipped, s => s.Name == "Pepper");
        }
    }
}
