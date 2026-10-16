using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using RecipeHelper;
using RecipeHelper.Models.IngredientModels;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Services;
using Xunit;

namespace RecipeHelper.Tests
{
    // Saving an imported recipe failed with "The instance of entity type
    // 'IngredientKrogerProduct' cannot be tracked because another instance with the same key
    // value for {'IngredientId', 'Upc'} is already being tracked". The save loop adds links
    // without saving until the end, and IngredientProductLinkExists only queried the
    // database -- so a recipe using the same ingredient + product on two lines (butter in
    // the cake and the frosting) added the same link twice.
    public class IngredientProductLinkPendingTests
    {
        [Fact]
        public async Task SameIngredientAndUpcTwice_InOneSave_AddsOneLink()
        {
            var db = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var butter = new Ingredient { CanonicalName = "butter" };
            db.Ingredients.Add(butter);
            db.KrogerProducts.Add(new KrogerProduct { Upc = "0001111", Name = "Butter" });
            await db.SaveChangesAsync();

            var service = new IngredientsService(db, NullLogger<IngredientsService>.Instance, new OpenAIClient("test-key"));

            // Same shape as ImportService.SaveImportedRecipe's per-ingredient loop.
            foreach (var _ in new[] { "cake", "frosting" })
            {
                if (!await service.IngredientProductLinkExists(butter.Id, "0001111"))
                {
                    db.Add(new IngredientKrogerProduct { IngredientId = butter.Id, Upc = "0001111" });
                }
            }

            await db.SaveChangesAsync();
            Assert.Equal(1, await db.IngredientKrogerProducts.CountAsync());
        }
    }
}
