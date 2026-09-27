using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RecipeHelper.Models;
using RecipeHelper.Models.Dinner;
using RecipeHelper.Services;

namespace RecipeHelper.Controllers
{
    public class ShareController : Controller
    {
        private readonly MealPlanService _mealPlanService;
        private readonly RecipeService _recipeService;

        public ShareController(MealPlanService mealPlanService, RecipeService recipeService)
        {
            _mealPlanService = mealPlanService;
            _recipeService = recipeService;
        }

        // GET: Share/MealPlan/{token} — public, read-only week view
        [HttpGet("Share/MealPlan/{token}")]
        public async Task<IActionResult> MealPlan(string token)
        {
            var plan = await _mealPlanService.GetByShareTokenAsync(token);
            if (plan == null) return NotFound();

            var vm = new ShareMealPlanVM
            {
                WeekStart = plan.WeekStartDate,
                Plan = plan,
            };

            return View(vm);
        }

        // GET: Share/Recipe/{token} — public, read-only recipe view
        [HttpGet("Share/Recipe/{token}")]
        public async Task<IActionResult> Recipe(string token)
        {
            var recipe = await _recipeService.GetByShareTokenAsync(token);
            if (recipe == null) return NotFound();

            var vm = new ShareRecipeVM
            {
                RecipeName = recipe.Name,
                ImageUri = recipe.ImageUri ?? "",
                ThumbnailUri = recipe.ThumbnailUri,
                Ingredients = recipe.Ingredients.OrderBy(ri => ri.SortOrder).Select(ri => new IngredientVM
                {
                    Name = ri.DisplayName,
                    Quantity = ri.Quantity,
                    Measurement = ri.Measurement.Name,
                    Section = ri.Section,
                }).ToList(),
                Instructions = string.IsNullOrEmpty(recipe.Instructions)
                    ? new()
                    : JsonSerializer.Deserialize<List<string>>(recipe.Instructions) ?? new()
            };

            return View(vm);
        }
    }
}
