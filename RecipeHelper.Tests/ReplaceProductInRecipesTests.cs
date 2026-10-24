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

namespace RecipeHelper.Tests
{
    // ViewProduct posted to DeleteProduct/UpdateProduct, which were removed from the
    // controller when products moved to UPC-keyed KrogerProducts -- the POST hit no action
    // and the user saw a blank page.
    public class DeleteProductTests
    {
        private static (ProductController ctl, DatabaseContext db) Setup()
        {
            var db = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            db.KrogerProducts.AddRange(new KrogerProduct { Upc = "USED", Name = "Used" }, new KrogerProduct { Upc = "FREE", Name = "Free" });
            var ing = new Ingredient { CanonicalName = "butter" };
            db.Ingredients.Add(ing);
            db.Recipes.Add(new Recipe { Id = 1, Name = "A" });
            db.SaveChanges();
            db.RecipeIngredients.Add(new RecipeIngredient { RecipeId = 1, IngredientId = ing.Id, DisplayName = "butter", SelectedKrogerUpc = "USED" });
            db.IngredientKrogerProducts.Add(new IngredientKrogerProduct { IngredientId = ing.Id, Upc = "FREE" });
            db.SaveChanges();
            var ctl = new ProductController(db, null!, NullLogger<ProductController>.Instance,
                new ProductService(db, NullLogger<ProductService>.Instance))
            { TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(new Microsoft.AspNetCore.Http.DefaultHttpContext(), new FakeTempData()) };
            return (ctl, db);
        }

        private class FakeTempData : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
        {
            public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext c) => new Dictionary<string, object>();
            public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext c, IDictionary<string, object> v) { }
        }

        [Fact]
        public async Task Delete_UnusedProduct_RemovesItAndItsMappings()
        {
            var (ctl, db) = Setup();
            var r = await ctl.DeleteProduct("FREE");
            Assert.Equal("Products", ((RedirectToActionResult)r).ActionName);
            Assert.False(await db.KrogerProducts.AnyAsync(p => p.Upc == "FREE"));
            Assert.Empty(await db.IngredientKrogerProducts.ToListAsync());
        }

        [Fact]
        public async Task Delete_ProductUsedInRecipe_UnlinksItFromTheRecipeLine()
        {
            var (ctl, db) = Setup();
            var r = await ctl.DeleteProduct("USED");
            Assert.Equal("Products", ((RedirectToActionResult)r).ActionName);
            Assert.False(await db.KrogerProducts.AnyAsync(p => p.Upc == "USED"));
            var line = await db.RecipeIngredients.SingleAsync();
            Assert.Null(line.SelectedKrogerUpc);
            Assert.Equal("butter", line.DisplayName);
        }


        [Fact]
        public async Task Delete_ClearsPantryUpc_AndPromotesAnotherDefaultMapping()
        {
            var (ctl, db) = Setup();
            var ing = await db.Ingredients.SingleAsync();
            // FREE is ing's default; add a second mapping that should take over.
            (await db.IngredientKrogerProducts.SingleAsync()).IsDefault = true;
            db.IngredientKrogerProducts.Add(new IngredientKrogerProduct { IngredientId = ing.Id, Upc = "USED", IsDefault = false });
            db.PantryItems.Add(new PantryItem { Name = "Butter", NormalizedName = "butter", KrogerUpc = "FREE" });
            await db.SaveChangesAsync();

            await ctl.DeleteProduct("FREE");

            var pantry = await db.PantryItems.SingleAsync();
            Assert.Null(pantry.KrogerUpc);
            Assert.Equal("butter", pantry.NormalizedName);
            var remaining = await db.IngredientKrogerProducts.SingleAsync();
            Assert.Equal("USED", remaining.Upc);
            Assert.True(remaining.IsDefault);
        }
        [Fact]
        public async Task Update_RenamesByUpc()
        {
            var (ctl, db) = Setup();
            await ctl.UpdateProduct("FREE", "  Better name ");
            Assert.Equal("Better name", (await db.KrogerProducts.FindAsync("FREE"))!.Name);
        }
    }
}
