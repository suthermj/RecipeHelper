using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RecipeHelper;
using RecipeHelper.Controllers;
using RecipeHelper.Models;
using RecipeHelper.Models.Dinner;
using RecipeHelper.Models.IngredientModels;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Services;
using Xunit;

namespace RecipeHelper.Tests
{
    // Reported: on the ingredient review page, minced garlic had to be unchecked once
    // per recipe. Each recipe names it slightly differently ("Garlic Cloves (Minced)",
    // "garlic minced", "garlic, minced") but all three are linked to the same Kroger
    // product. SubmitDinnerSelections merged rows by display name only, so those became
    // three separate rows (and three separate toggles) instead of one.
    public class ReviewIngredientsMergeByUpcTests
    {
        private const string GarlicUpc = "0001111041700";

        private sealed class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> _store = new();
            public bool IsAvailable => true;
            public string Id => "test";
            public IEnumerable<string> Keys => _store.Keys;
            public void Clear() => _store.Clear();
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void Remove(string key) => _store.Remove(key);
            public void Set(string key, byte[] value) => _store[key] = value;
            public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
        }

        private sealed class Envelope { public ReviewDinnerSelectionsVM? Value { get; set; } }

        private static async Task<ReviewDinnerSelectionsVM> Submit(Action<DatabaseContext> seed, params int[] recipeIds)
        {
            var db = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            seed(db);
            await db.SaveChangesAsync();

            var session = new TestSession();
            var controller = new DinnerController(NullLogger<RecipeController>.Instance, db,
                new MealPlanService(NullLogger<MealPlanService>.Instance, db))
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { Session = session } }
            };

            await controller.SubmitDinnerSelections(recipeIds.ToList());

            var json = session.GetString("PendingDinnerReview")!;
            return JsonSerializer.Deserialize<Envelope>(json)!.Value!;
        }

        private static void SeedGarlicRecipes(DatabaseContext db, string? thirdRecipeUpc = GarlicUpc)
        {
            var tsp = new Measurement { Id = 1, Name = "Teaspoons", MeasureType = "volume" };
            var unit = new Measurement { Id = 2, Name = "Unit", MeasureType = "count" };
            db.Measurements.AddRange(tsp, unit);
            db.KrogerProducts.Add(new KrogerProduct { Upc = GarlicUpc, Name = "Spice World Minced Garlic" });

            var garlic1 = new Ingredient { Id = 1, CanonicalName = "garlic cloves" };
            var garlic2 = new Ingredient { Id = 2, CanonicalName = "garlic" };
            var garlic3 = new Ingredient { Id = 3, CanonicalName = "garlic minced" };
            var onion = new Ingredient { Id = 4, CanonicalName = "onion" };
            db.Ingredients.AddRange(garlic1, garlic2, garlic3, onion);

            RecipeIngredient Ri(int id, Ingredient ing, string name, decimal qty, Measurement m, string? upc) =>
                new() { Id = id, Ingredient = ing, IngredientId = ing.Id, DisplayName = name, Quantity = qty, Measurement = m, MeasurementId = m.Id, SelectedKrogerUpc = upc };

            db.Recipes.AddRange(
                new Recipe { Id = 1, Name = "Chicken Stir Fry", Ingredients = { Ri(1, garlic1, "Garlic Cloves (Minced)", 4, tsp, GarlicUpc) } },
                new Recipe { Id = 2, Name = "Chili", Ingredients = { Ri(2, garlic2, "garlic minced", 1.5m, tsp, GarlicUpc) } },
                new Recipe { Id = 3, Name = "Mexican-Style Rice", Ingredients =
                {
                    Ri(3, garlic3, "garlic, minced", 2, tsp, thirdRecipeUpc),
                    Ri(4, onion, "onion, finely chopped", 1, unit, null),
                } });
        }

        [Fact]
        public async Task DifferentNames_SameUpc_MergeIntoOneRow()
        {
            var model = await Submit(db => SeedGarlicRecipes(db), 1, 2, 3);

            var garlic = Assert.Single(model.Ingredients, i => i.Upc == GarlicUpc);
            Assert.Equal(7.5m, garlic.Quantity); // 4 + 1.5 + 2 teaspoons
            Assert.Equal("Teaspoons", garlic.Measurement);
            Assert.Equal(2, model.Ingredients.Count); // garlic + onion

            // The by-recipe view syncs toggles by DimensionKey, so every recipe's garlic
            // row must share the merged row's key (and so shows as "(shared)").
            var recipeGarlicKeys = model.RecipeGroups
                .SelectMany(g => g.Ingredients)
                .Where(i => i.Name.Contains("arlic"))
                .Select(i => i.DimensionKey)
                .Distinct();
            Assert.Equal(garlic.DimensionKey, Assert.Single(recipeGarlicKeys));
        }

        [Fact]
        public async Task PantryItems_FlagMatchingRows_ByUpcAndWholeWordName()
        {
            var model = await Submit(db =>
            {
                SeedGarlicRecipes(db);
                db.PantryItems.AddRange(
                    new PantryItem { Name = "Garlic", NormalizedName = "garlic", KrogerUpc = GarlicUpc },
                    new PantryItem { Name = "onions", NormalizedName = "onions" });
            }, 1, 2, 3);

            Assert.True(Assert.Single(model.Ingredients, i => i.Upc == GarlicUpc).IsPantry);
            Assert.True(model.Ingredients.Single(i => i.Name.StartsWith("onion")).IsPantry); // plural-trimmed word match
            Assert.All(model.RecipeGroups.SelectMany(g => g.Ingredients), i => Assert.True(i.IsPantry));
        }

        [Fact]
        public async Task SameName_OneMappedOneNot_StillMergeWithTheMappedProduct()
        {
            // "garlic, minced" appears unmapped in one recipe but mapped in another.
            var model = await Submit(db =>
            {
                SeedGarlicRecipes(db, thirdRecipeUpc: null);
                var tsp = db.Measurements.Local.First(m => m.Name == "Teaspoons");
                var garlic3 = db.Ingredients.Local.First(i => i.Id == 3);
                db.Recipes.Add(new Recipe
                {
                    Id = 4, Name = "Tacos", Ingredients =
                    {
                        new RecipeIngredient { Id = 5, Ingredient = garlic3, IngredientId = 3, DisplayName = "garlic, minced", Quantity = 1, Measurement = tsp, MeasurementId = 1, SelectedKrogerUpc = GarlicUpc }
                    }
                });
            }, 1, 2, 3, 4);

            var garlic = Assert.Single(model.Ingredients, i => i.Name.Contains("arlic"));
            Assert.Equal(GarlicUpc, garlic.Upc);
            Assert.Equal(8.5m, garlic.Quantity);
        }

        [Fact]
        public async Task UnmappedIngredients_StillMergeByName()
        {
            var model = await Submit(db =>
            {
                var unit = new Measurement { Id = 2, Name = "Unit", MeasureType = "count" };
                db.Measurements.Add(unit);
                var onion = new Ingredient { Id = 4, CanonicalName = "onion" };
                db.Ingredients.Add(onion);
                db.Recipes.AddRange(
                    new Recipe { Id = 1, Name = "A", Ingredients = { new RecipeIngredient { Id = 1, Ingredient = onion, IngredientId = 4, DisplayName = "Onion", Quantity = 1, Measurement = unit, MeasurementId = 2 } } },
                    new Recipe { Id = 2, Name = "B", Ingredients = { new RecipeIngredient { Id = 2, Ingredient = onion, IngredientId = 4, DisplayName = "onion ", Quantity = 2, Measurement = unit, MeasurementId = 2 } } });
            }, 1, 2);

            var onionRow = Assert.Single(model.Ingredients);
            Assert.Equal(3, onionRow.Quantity);
        }
    }
}
