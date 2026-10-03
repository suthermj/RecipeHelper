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

        // Stands in for the AI parser: a line containing " and " comes back as two items
        // (the model splitting "Salt and pepper to taste"), so a batch miscounts.
        private static Task<List<IngredientsService.ParsedIngredientItem>> FakeParse(List<string> lines)
        {
            var known = new Dictionary<string, IngredientsService.ParsedIngredientItem[]>
            {
                ["Salt and pepper to taste"] = new[]
                {
                    new IngredientsService.ParsedIngredientItem { Name = "salt", Quantity = null, Unit = "unit" },
                    new IngredientsService.ParsedIngredientItem { Name = "pepper", Quantity = null, Unit = "unit" },
                },
                ["2 cups rice"] = new[] { new IngredientsService.ParsedIngredientItem { Name = "rice", Quantity = 2, Unit = "cup" } },
                ["1 lb ground beef"] = new[] { new IngredientsService.ParsedIngredientItem { Name = "ground beef", Quantity = 1, Unit = "lb" } },
            };
            return Task.FromResult(lines.SelectMany(l => known[l]).ToList());
        }

        [Fact]
        public async Task ReparseEditedLines_BatchMiscount_DoesNotShiftLaterLines()
        {
            var ingredients = new List<ImportedIngredient>
            {
                new() { Include = true, Name = "salt", Text = "Salt and pepper to taste", OriginalText = "Salt, to taste", IngredientId = 1 },
                new() { Include = true, Name = "rice", Text = "2 cups rice", OriginalText = "1 cup rice", Amount = 1, Unit = "cup", IngredientId = 2 },
                new() { Include = true, Name = "ground beef", Text = "1 lb ground beef", OriginalText = "2 lb ground beef", Amount = 2, Unit = "lb", IngredientId = 3 },
            };

            await ImportService.ReparseEditedLinesAsync(ingredients, FakeParse, NullLogger.Instance);

            Assert.Equal("salt", ingredients[0].Name);
            Assert.Equal(0m, ingredients[0].Amount);
            Assert.Equal(("rice", 2m, "cup", (int?)2), (ingredients[1].Name, ingredients[1].Amount, ingredients[1].Unit, ingredients[1].IngredientId));
            Assert.Equal(("ground beef", 1m, "lb", (int?)3), (ingredients[2].Name, ingredients[2].Amount, ingredients[2].Unit, ingredients[2].IngredientId));
        }

        [Fact]
        public void CreateEdit_LineCappedToColumnLength()
        {
            Assert.Equal(500, RecipeHelper.Controllers.RecipeController.TruncateLine(new string('x', 800))!.Length);
            Assert.Equal("1 cup butter, divided", RecipeHelper.Controllers.RecipeController.TruncateLine("  1 cup butter, divided "));
            Assert.Null(RecipeHelper.Controllers.RecipeController.TruncateLine("   "));
        }

        [Theory]
        [InlineData("10 Ounces chunky salsa", "10 Ounces", "chunky salsa")]
        [InlineData("1 Cup Shredded Lettuce", "1 Cup", "Shredded Lettuce")]
        [InlineData("1 1/2 cups all-purpose flour, sifted", "1 1/2 cups", "all-purpose flour, sifted")]
        [InlineData("1/2 tsp. cumin", "1/2 tsp.", "cumin")]
        [InlineData("½ cup sugar", "½ cup", "sugar")]
        [InlineData("2-3 cloves garlic", "2-3", "cloves garlic")]
        [InlineData("2 eggs", "2", "eggs")]
        [InlineData("1 (15 oz) can black beans", "1", "(15 oz) can black beans")]
        [InlineData("16 oz canned black beans", "16 oz", "canned black beans")]
        [InlineData("1 lb ground beef", "1 lb", "ground beef")]
        [InlineData("Salt, to taste", "", "Salt, to taste")]
        [InlineData("Olive oil, for frying", "", "Olive oil, for frying")]
        [InlineData("3 cups", "", "3 cups")]                         // nothing left for the name
        [InlineData("1 green onion", "1", "green onion")]           // "g" unit must not eat "green"
        [InlineData("2 large carrots", "2", "large carrots")]       // "l" unit must not eat "large"
        public void LineSplitter_SplitsLeadingAmount(string line, string amount, string rest)
        {
            Assert.Equal((amount, rest), IngredientLineSplitter.Split(line));
        }

        [Fact]
        public void IngredientVM_FreeFormLine_RendersInAmountAndNameColumns()
        {
            var typed = new IngredientVM { Name = "chunky salsa", Text = "10 Ounces chunky salsa", Quantity = 10, Measurement = "Ounces" };
            Assert.Equal(("10 Ounces", "chunky salsa"), (typed.AmountLabel, typed.NameLabel));

            var legacy = new IngredientVM { Name = "chicken broth", Quantity = 0.5m, Measurement = "Cups" };
            Assert.Equal(("1/2 Cups", "chicken broth"), (legacy.AmountLabel, legacy.NameLabel));
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
