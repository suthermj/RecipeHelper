using Microsoft.AspNetCore.Mvc;
using RecipeHelper.Models;
using RecipeHelper.Models.Dinner;
using RecipeHelper.Services;
using RecipeHelper.Utility;

namespace RecipeHelper.Controllers
{
    public class DinnerController : Controller
    {
        private readonly DatabaseContext _context;
        private readonly MealPlanService _mealPlanService;
        private readonly ILogger<RecipeController> _logger;

        public DinnerController(ILogger<RecipeController> logger, DatabaseContext context, MealPlanService mealPlanService)
        {
            _logger = logger;
            _context = context;
            _mealPlanService = mealPlanService;
        }

        // GET: Dinner — current week's plan
        public async Task<ActionResult> Index()
        {
            var plan = await _mealPlanService.GetCurrentWeekAsync();
            return View(plan);
        }

        // GET: Dinner/PlanWeek — 7-slot day picker
        public async Task<ActionResult> PlanWeek(int? id)
        {
            var weekStart = MealPlanService.GetWeekStart(DateTime.UtcNow);
            var vm = new PlanWeekVM
            {
                WeekStartDate = weekStart,
                AllRecipes = _context.Recipes.Select(r => new ViewRecipeVM
                {
                    Id = r.Id,
                    RecipeName = r.Name,
                    ImageUri = r.ImageUri,
                    DinnerCategory = r.DinnerCategory,
                }).ToList(),
            };

            if (id.HasValue)
            {
                var existing = await _mealPlanService.GetByIdAsync(id.Value);
                if (existing != null)
                {
                    vm.WeekStartDate = existing.WeekStartDate;
                    foreach (var entry in existing.Entries)
                    {
                        if (entry.DayOfWeek >= 0 && entry.DayOfWeek < 7)
                            vm.DayRecipes[entry.DayOfWeek] = entry.RecipeId;
                    }
                }
            }

            return View(vm);
        }

        // POST: Dinner/SaveMealPlan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SaveMealPlan(SaveMealPlanVM model)
        {
            await _mealPlanService.SaveAsync(model.WeekStartDate, model.DayRecipes);
            return RedirectToAction(nameof(Index));
        }

        // GET: Dinner/History
        public async Task<ActionResult> History()
        {
            var plans = await _mealPlanService.GetHistoryAsync();
            return View(plans);
        }

        // GET: Dinner/ViewPlan/5
        public async Task<ActionResult> ViewPlan(int id)
        {
            var plan = await _mealPlanService.GetByIdAsync(id);
            if (plan == null) return NotFound();
            return View(plan);
        }

        // POST: Dinner/DeletePlan/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeletePlan(int id)
        {
            await _mealPlanService.DeleteAsync(id);
            return RedirectToAction(nameof(History));
        }

        // GET: Dinner/SelectWeeklyRecipes — kept for ingredient review flow
        public ActionResult SelectWeeklyRecipes()
        {
            var recipes = _context.Recipes.Select(r => new ViewRecipeVM
            {
                Id = r.Id,
                RecipeName = r.Name,
                ImageUri = r.ImageUri,
                DinnerCategory = r.DinnerCategory,
                Ingredients = r.Ingredients.Select(rp => new IngredientVM
                {
                    Name = rp.DisplayName,
                    Quantity = rp.Quantity,
                }).ToList(),
            }).ToList();

            return View(recipes);
        }

        // POST: Dinner/SubmitDinnerSelections — ingredient aggregation, unchanged
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitDinnerSelections(List<int> selectedRecipes)
        {
            ReviewDinnerSelectionsVM model = new ReviewDinnerSelectionsVM
            {
                SelectedRecipes = new List<SelectedRecipeVM>(),
                Ingredients = new List<IngredientVM>()
            };

            var recipes = _context.Recipes.Where(r => selectedRecipes.Contains(r.Id)).Select(r => new ViewRecipeVM
            {
                RecipeName = r.Name,
                ImageUri = r.ImageUri,
                Ingredients = r.Ingredients.Select(rp => new IngredientVM
                {
                    Id = rp.IngredientId,
                    Name = rp.DisplayName,
                    Section = rp.Section,
                    Quantity = rp.Quantity,
                    Upc = rp.SelectedKrogerUpc,
                    Measurement = rp.Measurement.Name == null ? "Count" : rp.Measurement.Name
                }).ToList(),
            }).ToList();

            Dictionary<int, List<IngredientVM>> ingDict = new Dictionary<int, List<IngredientVM>>();
            List<IngredientVM> tempIngredients = new List<IngredientVM>();

            if (recipes != null)
            {
                foreach (var recipe in recipes)
                {
                    model.SelectedRecipes.Add(new SelectedRecipeVM
                    {
                        RecipeName = recipe.RecipeName,
                        ImageUri = recipe.ImageUri
                    });

                    foreach (var ingredient in recipe.Ingredients)
                    {
                        tempIngredients.Add(ingredient);
                        if (ingDict.ContainsKey(ingredient.Id))
                        {
                            ingDict[ingredient.Id].Add(new IngredientVM
                            {
                                Id = ingredient.Id,
                                Name = ingredient.Name,
                                Section = ingredient.Section,
                                Quantity = ingredient.Quantity,
                                Upc = ingredient.Upc,
                                Measurement = ingredient.Measurement
                            });
                        }
                        else
                        {
                            List<IngredientVM> ingredientList =
                            [
                                new IngredientVM
                                {
                                    Id = ingredient.Id,
                                    Name = ingredient.Name,
                                    Section = ingredient.Section,
                                    Quantity = ingredient.Quantity,
                                    Upc = ingredient.Upc,
                                    Measurement = ingredient.Measurement
                                },
                            ];
                            ingDict.Add(ingredient.Id, ingredientList);
                        }
                    }
                }
            }

            foreach (var ingredient in ingDict)
            {
                _logger.LogInformation("Processing ingredient ID: {ingredientId} with {count} entries", ingredient.Key, ingredient.Value.Count);
                bool allSame = ingredient.Value.All(x => x.Measurement.Equals(ingredient.Value[0].Measurement));

                if (allSame)
                {
                    decimal totalQuantity = ingredient.Value.Sum(x => x.Quantity);
                    model.Ingredients.Add(new IngredientVM
                    {
                        Id = ingredient.Key,
                        Name = ingredient.Value[0].Name,
                        Section = ingredient.Value[0].Section,
                        Quantity = totalQuantity,
                        Upc = ingredient.Value[0].Upc,
                        Measurement = ingredient.Value[0].Measurement
                    });
                }
                else
                {
                    decimal totalVolumeBase = 0;
                    decimal totalWeightBase = 0;
                    decimal totalUnits = 0;
                    bool hasVolume = false, hasWeight = false, hasUnit = false;

                    foreach (var entry in ingredient.Value)
                    {
                        var mu = UnitConverter.Parse(entry.Measurement);
                        var dim = UnitConverter.GetDimension(mu);

                        switch (dim)
                        {
                            case MeasureDimension.Volume:
                                totalVolumeBase += UnitConverter.ToBase(entry.Quantity, mu) ?? 0;
                                hasVolume = true;
                                break;
                            case MeasureDimension.Weight:
                                totalWeightBase += UnitConverter.ToBase(entry.Quantity, mu) ?? 0;
                                hasWeight = true;
                                break;
                            default:
                                totalUnits += entry.Quantity;
                                hasUnit = true;
                                break;
                        }
                    }

                    if (hasVolume)
                    {
                        var (displayQty, displayName) = UnitConverter.PickBestVolumeDisplay(totalVolumeBase);
                        model.Ingredients.Add(new IngredientVM
                        {
                            Id = ingredient.Key,
                            Name = ingredient.Value[0].Name,
                            Section = ingredient.Value[0].Section,
                            Quantity = displayQty,
                            Upc = ingredient.Value[0].Upc,
                            Measurement = displayName
                        });
                    }

                    if (hasWeight)
                    {
                        var (displayQty, displayName) = UnitConverter.PickBestWeightDisplay(totalWeightBase);
                        model.Ingredients.Add(new IngredientVM
                        {
                            Id = ingredient.Key,
                            Name = ingredient.Value[0].Name,
                            Section = ingredient.Value[0].Section,
                            Quantity = displayQty,
                            Upc = ingredient.Value[0].Upc,
                            Measurement = displayName
                        });
                    }

                    if (hasUnit)
                    {
                        model.Ingredients.Add(new IngredientVM
                        {
                            Id = ingredient.Key,
                            Name = ingredient.Value[0].Name,
                            Section = ingredient.Value[0].Section,
                            Quantity = totalUnits,
                            Upc = ingredient.Value[0].Upc,
                            Measurement = "Unit"
                        });
                    }
                }
            }

            return View("ReviewDinnerSelections", model);
        }
    }
}
