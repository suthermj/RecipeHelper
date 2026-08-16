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
    // Per user request: the cart preview's per-row warning shouldn't expose the
    // mechanism-specific reasoning behind a fallback/estimate (which dimension
    // mismatched, whether a density was assumed, etc.) -- that detail stays in the
    // server logs, and every ConversionNote a shopper sees uses the same short,
    // non-technical text regardless of which branch produced it.
    public class ConversionNoteWordingTests
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
        public void DifferentFallbackBranches_ProduceIdenticalConversionNoteText()
        {
            var service = BuildService();

            // Branch A: unparseable size (Volume ingredient -> defaults to 1).
            var unparseableProduct = new KrogerProductDto
            {
                upc = "0001010101010", name = "Mystery Sauce", size = "Varies", soldBy = "UNIT",
                categories = new List<string>(), stockLevel = "HIGH",
            };
            var resultA = service.ConvertResolvedItemsToCartItems(new List<(CartItemVM, KrogerProductDto)>
            {
                (new CartItemVM { Name = "sauce", Upc = unparseableProduct.upc, Quantity = 3, Measurement = "Cups", Include = true }, unparseableProduct),
            });

            // Branch B: cross-dimension density fallback (no DensityTable entry for
            // "Mystery Powder", so a density is assumed).
            var crossDimProduct = new KrogerProductDto
            {
                upc = "0002020202020", name = "Mystery Powder", size = "10 oz", soldBy = "UNIT",
                categories = new List<string>(), stockLevel = "HIGH",
            };
            var resultB = service.ConvertResolvedItemsToCartItems(new List<(CartItemVM, KrogerProductDto)>
            {
                (new CartItemVM { Name = "powder", Upc = crossDimProduct.upc, Quantity = 1, Measurement = "Teaspoons", Include = true }, crossDimProduct),
            });

            var noteA = Assert.Single(resultA).ConversionNote;
            var noteB = Assert.Single(resultB).ConversionNote;

            Assert.NotNull(noteA);
            Assert.Equal(noteA, noteB);

            // Short and free of internal mechanism vocabulary a shopper shouldn't need.
            Assert.DoesNotContain("dimension", noteA, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("density", noteA, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ratio", noteA, StringComparison.OrdinalIgnoreCase);
            Assert.True(noteA!.Length < 40, $"Expected a short note, got: \"{noteA}\"");
        }
    }
}
