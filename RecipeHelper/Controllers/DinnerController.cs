using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RecipeHelper.Models;
using RecipeHelper.Models.Dinner;
using RecipeHelper.Services;
using RecipeHelper.Utility;

namespace RecipeHelper.Controllers
{
    public class DinnerController : Controller
    {
        private const string PendingReviewSessionKey = "PendingDinnerReview";

        private readonly DatabaseContext _context;
        private readonly MealPlanService _mealPlanService;
        private readonly ILogger<RecipeController> _logger;

        public DinnerController(ILogger<RecipeController> logger, DatabaseContext context, MealPlanService mealPlanService)
        {
            _logger = logger;
            _context = context;
            _mealPlanService = mealPlanService;
        }

        // GET: Dinner — plan for a week with inline day picker
        public async Task<ActionResult> Index(DateTime? weekStart = null)
        {
            var week = MealPlanService.GetWeekStart(weekStart ?? MealPlanService.LocalToday());
            var plan = await _mealPlanService.GetByWeekAsync(week);

            var vm = new MealPlanIndexVM
            {
                WeekStart = week,
                Plan = plan,
                AllRecipes = _context.Recipes.Select(r => new ViewRecipeVM
                {
                    Id = r.Id,
                    RecipeName = r.Name,
                    ImageUri = r.ImageUri,
                    DinnerCategory = r.DinnerCategory,
                }).ToList(),
            };

            return View(vm);
        }

        private object BuildPlanJson(MealPlan? plan) => new
        {
            planId = plan?.Id,
            entries = plan?.Entries.Select(e => new
            {
                entryId = e.Id,
                dayOfWeek = e.DayOfWeek,
                recipeId = e.RecipeId,
                name = e.Recipe?.Name ?? "",
                img = e.Recipe?.ImageUri ?? ""
            }).ToArray() ?? Array.Empty<object>()
        };

        // POST: Dinner/AddDayRecipe — append a recipe entry to a day slot
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDayRecipe(DateTime weekStart, int dayOfWeek, int recipeId)
        {
            var week = MealPlanService.GetWeekStart(weekStart);
            var plan = await _mealPlanService.AddEntryAsync(week, dayOfWeek, recipeId);
            return Json(BuildPlanJson(plan));
        }

        // POST: Dinner/RemoveEntry — remove a single entry by id
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveEntry(int entryId)
        {
            var plan = await _mealPlanService.RemoveEntryAsync(entryId);
            return Json(BuildPlanJson(plan));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveEntry(int entryId, int dayOfWeek)
        {
            var plan = await _mealPlanService.MoveEntryAsync(entryId, dayOfWeek);
            if (plan == null) return NotFound();
            return Json(BuildPlanJson(plan));
        }

        // POST: Dinner/DeletePlan/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeletePlan(int id, DateTime? weekStart = null)
        {
            await _mealPlanService.DeleteAsync(id);
            return RedirectToAction(nameof(Index), weekStart.HasValue
                ? new { weekStart = weekStart.Value.ToString("yyyy-MM-dd") }
                : null);
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

        // POST: Dinner/SubmitDinnerSelections — ingredient aggregation
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitDinnerSelections(List<int> selectedRecipes)
        {
            ReviewDinnerSelectionsVM model = new ReviewDinnerSelectionsVM
            {
                SelectedRecipes = new List<SelectedRecipeVM>(),
                Ingredients = new List<IngredientVM>()
            };

            // Count occurrences so a recipe on multiple days multiplies its ingredient quantities
            var idCounts = selectedRecipes.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
            var distinctIds = idCounts.Keys.ToList();

            var recipeRows = _context.Recipes.Where(r => distinctIds.Contains(r.Id)).Select(r => new ViewRecipeVM
            {
                Id = r.Id,
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

            // Expand each recipe by its occurrence count before aggregation
            var recipes = new List<ViewRecipeVM>();
            foreach (var row in recipeRows)
            {
                int n = idCounts[row.Id];
                for (int i = 0; i < n; i++)
                    recipes.Add(row);
            }

            // Keyed by normalized display name rather than canonical Ingredient.Id --
            // two recipes' ingredients can resolve to different DB rows that both
            // display as the same name (a canonicalization gap on import), and those
            // need to merge here even though their Ids differ. ReviewDinnerSelections
            // only posts ingredients onward by Name/Upc/Quantity/Measurement, never by
            // Id, so this key change doesn't affect anything downstream.
            Dictionary<string, List<IngredientVM>> ingDict = new Dictionary<string, List<IngredientVM>>();
            List<IngredientVM> tempIngredients = new List<IngredientVM>();

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
                    var nameKey = ingredient.Name.Trim().ToLowerInvariant();
                    if (ingDict.ContainsKey(nameKey))
                    {
                        ingDict[nameKey].Add(new IngredientVM
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
                        ingDict.Add(nameKey, ingredientList);
                    }
                }
            }

            foreach (var ingredient in ingDict)
            {
                _logger.LogInformation("Processing ingredient: {ingredientName} with {count} entries", ingredient.Key, ingredient.Value.Count);
                bool allSame = ingredient.Value.All(x => x.Measurement.Equals(ingredient.Value[0].Measurement));

                if (allSame)
                {
                    decimal totalQuantity = ingredient.Value.Sum(x => x.Quantity);
                    model.Ingredients.Add(new IngredientVM
                    {
                        Id = ingredient.Value[0].Id,
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
                            Id = ingredient.Value[0].Id,
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
                            Id = ingredient.Value[0].Id,
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
                            Id = ingredient.Value[0].Id,
                            Name = ingredient.Value[0].Name,
                            Section = ingredient.Value[0].Section,
                            Quantity = totalUnits,
                            Upc = ingredient.Value[0].Upc,
                            Measurement = "Unit"
                        });
                    }
                }
            }

            // Dictionary iteration order isn't guaranteed, so sort explicitly by name --
            // this keeps an ingredient's split-dimension rows (e.g. a count-based entry
            // and a volume-based entry that couldn't be summed into one number) adjacent
            // in the list instead of scattered wherever other ingredients happen to fall.
            model.Ingredients = model.Ingredients
                .OrderBy(i => i.Section)
                .ThenBy(i => i.Name)
                .ToList();

            // Redirect-after-post: this page needs to be safely reloadable (e.g. after
            // tapping the PWA's "update available" banner), which a page rendered
            // directly from a POST is not -- a reload would resubmit the POST instead
            // of just re-fetching the page. Stash the computed review in session and
            // hand off to the GET action below instead.
            HttpContext.Session.SetString(PendingReviewSessionKey, JsonSerializer.Serialize(model));
            return RedirectToAction(nameof(ReviewDinnerSelections));
        }

        // GET: Dinner/ReviewDinnerSelections -- renders the review computed by
        // SubmitDinnerSelections above. Also the redirect target CartController uses
        // when it needs to bounce the user back here (e.g. empty cart, expired auth).
        [HttpGet]
        public ActionResult ReviewDinnerSelections()
        {
            var json = HttpContext.Session.GetString(PendingReviewSessionKey);
            if (string.IsNullOrEmpty(json))
            {
                return RedirectToAction(nameof(SelectWeeklyRecipes));
            }

            var model = JsonSerializer.Deserialize<ReviewDinnerSelectionsVM>(json);
            return View(model);
        }
    }
}
