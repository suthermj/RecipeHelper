using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RecipeHelper;
using RecipeHelper.Models;
using RecipeHelper.Models.Import;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Services;
using RecipeHelper.Utility;
using Xunit;

namespace RecipeHelper.Tests
{
    // Ingredients are shown as the line the recipe wrote ("Salt, to taste"); the parsed
    // amount only drives shopping math, with 0 meaning "no set amount".
    public class FreeFormIngredientTextTests
    {
        [Theory]
        [InlineData(1.5, "cup", "flour", "1.5 cup flour")]
        [InlineData(2.0, "Unit", "eggs", "2 eggs")]
        [InlineData(2.0, null, "eggs", "2 eggs")]
        [InlineData(0.0, "Unit", "salt", "salt")]
        [InlineData(null, "tsp", "salt", "salt")]
        public void ComposeIngredientLine_FallbackWhenImporterHasNoText(double? amount, string? unit, string name, string expected)
        {
            Assert.Equal(expected, MappingExtensions.ComposeIngredientLine((decimal?)amount, unit, name));
        }

        [Fact]
        public void ToVm_PrefersImporterLine_AndSeedsOriginalText()
        {
            var vm = new ImportPreview
            {
                Title = "Stuffed Peppers",
                Ingredients =
                {
                    new ImportPreviewIngredient { Name = "salt", Text = "Salt, to taste", Amount = 0, Unit = "Unit" },
                    new ImportPreviewIngredient { Name = "butter", Amount = 1, Unit = "cup" },
                }
            }.ToVm();

            Assert.Equal("Salt, to taste", vm.Ingredients[0].Text);
            Assert.Equal("Salt, to taste", vm.Ingredients[0].OriginalText);
            Assert.Equal("1 cup butter", vm.Ingredients[1].Text);
        }

        [Theory]
        [InlineData("Salt, to taste", "Salt, to taste", false)]
        [InlineData("  Salt, to taste ", "Salt, to taste", false)]
        [InlineData("1 tsp salt", "Salt, to taste", true)]
        [InlineData("1 can diced tomatoes", "", true)]   // row added on the mapping page
        [InlineData("", "Salt, to taste", false)]
        public void IsEditedLine_OnlyChangedLinesAreReparsed(string text, string original, bool expected)
        {
            var ing = new ImportedIngredient { Text = text, OriginalText = original, Include = true };
            Assert.Equal(expected, ImportService.IsEditedLine(ing));
        }

        [Fact]
        public void IngredientVM_NoSetAmount_RendersNoNumber()
        {
            var salt = new IngredientVM { Name = "salt", Quantity = 0, Measurement = "Unit" };
            Assert.False(salt.HasAmount);
            Assert.Equal("", salt.DisplayQuantity);
            Assert.Equal("", salt.DisplayMeasurement);

            var butter = new IngredientVM { Name = "butter", Quantity = 1, Measurement = "Cups" };
            Assert.True(butter.HasAmount);
            Assert.Equal("1", butter.DisplayQuantity);
            Assert.Equal("Cup", butter.DisplayMeasurement);
        }

        private sealed class ThrowingHttpClientFactory : IHttpClientFactory
        {
            public HttpClient CreateClient(string name) => throw new InvalidOperationException("no HTTP in this test");
        }

        private static KrogerService BuildKrogerService()
        {
            var db = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var config = new ConfigurationBuilder().Build();
            var auth = new KrogerAuthService(db, new ThrowingHttpClientFactory(), config,
                new Microsoft.AspNetCore.Http.HttpContextAccessor(), NullLogger<KrogerAuthService>.Instance);
            return new KrogerService(new ThrowingHttpClientFactory(), config, NullLogger<KrogerService>.Instance,
                auth, new MemoryCache(new MemoryCacheOptions()), new Microsoft.AspNetCore.Http.HttpContextAccessor());
        }

        [Fact]
        public void Cart_NoSetAmount_CheckedRowBuysOnePack_AndSaysSo()
        {
            var salt = new KrogerProductDto
            {
                upc = "0007800000001", name = "Morton Kosher Salt", brand = "Morton", size = "16 oz",
                soldBy = "UNIT", categories = new List<string> { "Baking Goods" }, stockLevel = "HIGH",
            };
            var resolved = new List<(CartItemVM Item, KrogerProductDto Product)>
            {
                (new CartItemVM { Name = "salt", Upc = salt.upc, Quantity = 0, Measurement = "Unit", Include = true }, salt),
            };

            var row = Assert.Single(BuildKrogerService().ConvertResolvedItemsToCartItems(resolved));

            Assert.Equal(1, row.Quantity);
            Assert.Equal("no set amount", row.OriginalIngredient);
        }
    }
}
