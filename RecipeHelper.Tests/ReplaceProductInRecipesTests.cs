using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RecipeHelper;
using RecipeHelper.Controllers;
using RecipeHelper.Models;
using RecipeHelper.Models.IngredientModels;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Services;
using Xunit;

namespace RecipeHelper.Tests
{
    // ProductController.ReplaceProductInRecipes re-points RecipeIngredient.SelectedKrogerUpc
    // for the chosen recipes only (every matching line within them, none outside).
    public class ReplaceProductInRecipesTests
    {
        private static (ProductController ctl, DatabaseContext db) Setup()
        {
            var db = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            db.KrogerProducts.AddRange(
                new KrogerProduct { Upc = "OLD", Name = "Old butter" },
                new KrogerProduct { Upc = "NEW", Name = "New butter" });
            var ing = new Ingredient { CanonicalName = "butter" };
            db.Ingredients.Add(ing);
            db.Recipes.AddRange(new Recipe { Id = 1, Name = "A" }, new Recipe { Id = 2, Name = "B" }, new Recipe { Id = 3, Name = "C" });
            db.SaveChanges();
            // Recipe 1 uses OLD on two lines, 2 and 3 on one each.
            foreach (var recipeId in new[] { 1, 1, 2, 3 })
                db.RecipeIngredients.Add(new RecipeIngredient { RecipeId = recipeId, IngredientId = ing.Id, DisplayName = "butter", SelectedKrogerUpc = "OLD" });
            db.SaveChanges();

            var ctl = new ProductController(db, null!, NullLogger<ProductController>.Instance,
                new ProductService(db, NullLogger<ProductService>.Instance));
            return (ctl, db);
        }

        [Fact]
        public async Task ReplacesOnlyInSelectedRecipes_AllLinesWithin()
        {
            var (ctl, db) = Setup();

            var result = await ctl.ReplaceProductInRecipes(new ProductController.ReplaceProductRequest
            {
                OldUpc = "OLD", NewUpc = "NEW", RecipeIds = new() { 1, 2 }
            });

            Assert.IsType<JsonResult>(result);
            var lines = await db.RecipeIngredients.AsNoTracking().ToListAsync();
            var byRecipe = lines.GroupBy(r => r.RecipeId).ToDictionary(g => g.Key, g => g.Select(x => x.SelectedKrogerUpc).ToList());
            Assert.All(byRecipe[1], u => Assert.Equal("NEW", u));
            Assert.All(byRecipe[2], u => Assert.Equal("NEW", u));
            Assert.All(byRecipe[3], u => Assert.Equal("OLD", u));
        }

        [Fact]
        public async Task SameProduct_Or_NoRecipes_IsRejected_AndChangesNothing()
        {
            var (ctl, db) = Setup();

            var same = await ctl.ReplaceProductInRecipes(new ProductController.ReplaceProductRequest { OldUpc = "OLD", NewUpc = "OLD", RecipeIds = new() { 1 } });
            var none = await ctl.ReplaceProductInRecipes(new ProductController.ReplaceProductRequest { OldUpc = "OLD", NewUpc = "NEW", RecipeIds = new() });

            Assert.IsType<BadRequestObjectResult>(same);
            Assert.IsType<BadRequestObjectResult>(none);
            Assert.Equal(4, await db.RecipeIngredients.CountAsync(r => r.SelectedKrogerUpc == "OLD"));
        }
    }
}
