using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models;
using RecipeHelper.Models.RecipeModels;
using RecipeHelper.Services;
using RecipeHelper.Utility;
using RecipeHelper.ViewModels;


namespace RecipeHelper.Controllers
{
    public class RecipeController : Controller
    {
        private DatabaseContext _context;

        private readonly ILogger<RecipeController> _logger;
        private StorageService _storageService;
        private SpoonacularService _spoonacularService;
        private RecipeService _recipeService;
        private IngredientsService _ingredientsService;

        public RecipeController(ILogger<RecipeController> logger, DatabaseContext context, StorageService storageService, SpoonacularService spoonacularService, RecipeService recipeService, IngredientsService ingredientsService)
        {
            _logger = logger;
            _context = context;
            _storageService = storageService;
            _spoonacularService = spoonacularService;
            _recipeService = recipeService;
            _ingredientsService = ingredientsService;
        }

        public ActionResult Recipe()
        {

            var recipes = _context.Recipes.Select(r => new ViewRecipeVM
            {
                Id = r.Id,
                RecipeName = r.Name,
                ImageUri = r.ImageUri,
                Ingredients = r.Ingredients.Select(rp => new IngredientVM
                {
                    Name = rp.DisplayName,
                    Quantity = rp.Quantity,
                    Measurement = rp.Measurement.Name,
                }).ToList(),
            }).ToList();

            return View(recipes);
        }

        public ActionResult ViewRecipe(int Id)
        {
            var data = _context.Recipes.Where(r => r.Id == Id).Select(r => new
            {
                r.Id,
                r.Name,
                r.ImageUri,
                r.Instructions,
                Ingredients = r.Ingredients.Select(rp => new IngredientVM
                {
                    Name = rp.DisplayName,
                    Quantity = rp.Quantity,
                    Measurement = rp.Measurement.Name,
                }).ToList(),
            }).FirstOrDefault();

            if (data == null) return RedirectToAction("Recipe");

            var recipe = new ViewRecipeVM
            {
                Id = data.Id,
                RecipeName = data.Name,
                ImageUri = data.ImageUri,
                Ingredients = data.Ingredients,
                Instructions = string.IsNullOrEmpty(data.Instructions)
                    ? new()
                    : JsonSerializer.Deserialize<List<string>>(data.Instructions) ?? new()
            };

            return View(recipe);
        }

        public IActionResult SaveRecipe(ViewRecipeVM model)
        {
            return RedirectToAction("Recipe");
        }

        // Returns create recipe view or shows current recipe if id is not null
        [HttpGet]
        public async Task<ActionResult> CreateEditRecipe(int? id)
        {
            if (id == null)
            {
                return View("Create", new CreateRecipeVM());
            }
            else
            {
                var data = await _context.Recipes.Where(r => r.Id == id).Select(r => new
                {
                    r.Id,
                    r.Name,
                    r.ImageUri,
                    r.Instructions,
                    Ingredients = r.Ingredients.Select(rp => new
                    {
                        rp.Id,
                        rp.DisplayName,
                        rp.Quantity,
                        MeasurementName = rp.Measurement.Name,
                        rp.SelectedKrogerUpc,
                        rp.IngredientId
                    }).ToList(),
                }).FirstOrDefaultAsync();

                if (data == null) return RedirectToAction("Recipe");

                var recipe = new EditRecipeVM
                {
                    RecipeId = data.Id,
                    Title = data.Name,
                    ImageUri = data.ImageUri,
                    Ingredients = data.Ingredients.Select(rp => new EditRecipeIngredientVM
                    {
                        Id = rp.Id,
                        RawText = FormatIngredientText(rp.Quantity, rp.MeasurementName, rp.DisplayName),
                        SelectedKrogerUpc = rp.SelectedKrogerUpc,
                        IngredientId = rp.IngredientId
                    }).ToList(),
                    Instructions = string.IsNullOrEmpty(data.Instructions)
                        ? new()
                        : JsonSerializer.Deserialize<List<string>>(data.Instructions) ?? new()
                };

                return View("Edit", recipe);
            }
        }

        private static string FormatIngredientText(decimal quantity, string measurementName, string displayName)
        {
            var qtyStr = quantity % 1 == 0 ? ((int)quantity).ToString() : quantity.ToString("0.##");
            if (string.Equals(measurementName, "Unit", StringComparison.OrdinalIgnoreCase))
                return $"{qtyStr} {displayName}".Trim();
            return $"{qtyStr} {measurementName} {displayName}".Trim();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateRecipe(CreateRecipeVM vm)
        {
            var normalizedTitle = vm.Title?.Trim();

            if (string.IsNullOrWhiteSpace(normalizedTitle))
            {
                ModelState.AddModelError(nameof(vm.Title), "Recipe title is required.");
                return View("Create", vm);
            }

            var recipeExists = await _recipeService.RecipeNameExists(normalizedTitle);

            if (recipeExists)
            {
                ModelState.AddModelError(nameof(vm.Title), "A recipe with this title already exists. Please choose a different title.");
                return View("Create", vm);
            }

            var rawLines = vm.Ingredients
                .Where(i => !string.IsNullOrWhiteSpace(i.RawText))
                .Select(i => i.RawText.Trim())
                .ToList();

            var krogerUpcs = vm.Ingredients
                .Where(i => !string.IsNullOrWhiteSpace(i.RawText))
                .Select(i => i.SelectedKrogerUpc)
                .ToList();

            var ingredientDtos = await ParseRawLinesToDtos(rawLines, krogerUpcs);

            var request = new CreateRecipeRequest
            {
                Title = normalizedTitle,
                ImageFile = vm.ImageFile,
                Ingredients = ingredientDtos.Select(d => new CreateRecipeIngredientDto
                {
                    DisplayName = d.DisplayName,
                    Quantity = d.Quantity,
                    MeasurementId = d.MeasurementId,
                    SelectedKrogerUpc = d.KrogerUpc
                }).ToList(),
                Instructions = vm.Instructions ?? new()
            };

            var recipe = await _recipeService.CreateRecipe(request);

            return RedirectToAction("ViewRecipe", new { Id = recipe.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditRecipe(EditRecipeVM vm)
        {
            var rawLines = vm.Ingredients
                .Where(i => !string.IsNullOrWhiteSpace(i.RawText))
                .Select(i => i.RawText.Trim())
                .ToList();

            var krogerUpcs = vm.Ingredients
                .Where(i => !string.IsNullOrWhiteSpace(i.RawText))
                .Select(i => i.SelectedKrogerUpc)
                .ToList();

            var ingredientDtos = await ParseRawLinesToDtos(rawLines, krogerUpcs);

            var request = new EditRecipeRequest
            {
                Id = vm.RecipeId,
                Title = (vm.Title ?? "").Trim(),
                ImageFile = vm.ImageFile,
                Ingredients = ingredientDtos.Select(d => new EditRecipeIngredientDto
                {
                    Id = 0, // re-resolved on save
                    DisplayName = d.DisplayName,
                    Quantity = d.Quantity,
                    MeasurementId = d.MeasurementId,
                    IngredientId = 0, // re-resolved on save
                    SelectedKrogerUpc = d.KrogerUpc
                }).ToList(),
                Instructions = vm.Instructions ?? new()
            };

            await _recipeService.UpdateRecipeAsync(request);

            return RedirectToAction("ViewRecipe", new { Id = vm.RecipeId });
        }

        private async Task<List<ParsedIngredientDto>> ParseRawLinesToDtos(List<string> rawLines, List<string?> krogerUpcs)
        {
            if (!rawLines.Any()) return new();

            var measurements = await _context.Measurements.ToListAsync();
            var parsed = await _ingredientsService.TransformRawIngredients(rawLines, CancellationToken.None);

            var results = new List<ParsedIngredientDto>();

            for (int i = 0; i < parsed.Items.Count; i++)
            {
                var item = parsed.Items[i];
                var upc = i < krogerUpcs.Count ? krogerUpcs[i] : null;

                // Resolve unit string → MeasurementId
                var unit = UnitConverter.Parse(item.Unit ?? "unit");
                var displayName = UnitConverter.ToDisplayName(unit);
                var measurement = measurements.FirstOrDefault(m =>
                    string.Equals(m.Name, displayName, StringComparison.OrdinalIgnoreCase));

                // Fall back to "Unit" if not found in DB
                measurement ??= measurements.First(m =>
                    string.Equals(m.Name, "Unit", StringComparison.OrdinalIgnoreCase));

                results.Add(new ParsedIngredientDto
                {
                    DisplayName = item.Name ?? "",
                    Quantity = item.Quantity ?? 1m,
                    MeasurementId = measurement.Id,
                    KrogerUpc = upc
                });
            }

            return results;
        }

        private class ParsedIngredientDto
        {
            public string DisplayName { get; set; } = "";
            public decimal Quantity { get; set; }
            public int MeasurementId { get; set; }
            public string? KrogerUpc { get; set; }
        }

        [HttpPost("{id}")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteRecipe(int id)
        {
            _logger.LogInformation("[DeleteRecipe] Finding recipe with id [{id}]", id);
            var recipe = await _context.Recipes.FindAsync(id);

            if (recipe != null)
            {
                _logger.LogInformation("[DeleteRecipe] Found recipe with id [{id}]", id);
                try
                {
                    _context.Recipes.Remove(recipe);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, "Error deleting recipe with id [{id}]", id);
                    return RedirectToAction("Recipe");
                }
                _logger.LogInformation("[DeleteRecipe] Deleted recipe [{recipeName}] with id [{id}]", recipe.Name, id);

                if (recipe.ImageUri != null)
                {
                    var splitImageUri = recipe.ImageUri.Split("/");
                    string fileName = splitImageUri[splitImageUri.Length - 1];
                    await _storageService.DeleteImageRecipe(fileName);
                }

                return RedirectToAction("Recipe");
            }
            else
            {
                _logger.LogInformation("[DeleteRecipe] recipe with id [{id}] not found", id);
                return RedirectToAction("Recipe");
            }
        }



    }
}
