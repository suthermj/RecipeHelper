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
                DinnerCategory = r.DinnerCategory,
                Ingredients = r.Ingredients.OrderBy(rp => rp.SortOrder).Select(rp => new IngredientVM
                {
                    Name = rp.DisplayName,
                    Quantity = rp.Quantity,
                    Measurement = rp.Measurement.Name,
                    Section = rp.Section,
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
                r.DinnerCategory,
                r.SourceUrl,
                Ingredients = r.Ingredients.OrderBy(rp => rp.SortOrder).Select(rp => new IngredientVM
                {
                    Name = rp.DisplayName,
                    Quantity = rp.Quantity,
                    Measurement = rp.Measurement.Name,
                    Section = rp.Section,
                }).ToList(),
            }).FirstOrDefault();

            if (data == null) return RedirectToAction("Recipe");

            var recipe = new ViewRecipeVM
            {
                Id = data.Id,
                RecipeName = data.Name,
                ImageUri = data.ImageUri,
                DinnerCategory = data.DinnerCategory,
                SourceUrl = data.SourceUrl,
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
                    r.DinnerCategory,
                    Ingredients = r.Ingredients.OrderBy(rp => rp.SortOrder).Select(rp => new
                    {
                        rp.Id,
                        rp.DisplayName,
                        rp.Quantity,
                        MeasurementName = rp.Measurement.Name,
                        rp.SelectedKrogerUpc,
                        rp.IngredientId,
                        rp.Section
                    }).ToList(),
                }).FirstOrDefaultAsync();

                if (data == null) return RedirectToAction("Recipe");

                var recipe = new EditRecipeVM
                {
                    RecipeId = data.Id,
                    Title = data.Name,
                    DinnerCategory = data.DinnerCategory,
                    ImageUri = data.ImageUri,
                    Ingredients = data.Ingredients.Select(rp => new EditRecipeIngredientVM
                    {
                        Id = rp.Id,
                        RawText = FormatIngredientText(rp.Quantity, rp.MeasurementName, rp.DisplayName),
                        SelectedKrogerUpc = rp.SelectedKrogerUpc,
                        IngredientId = rp.IngredientId,
                        Section = rp.Section
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

            var filteredCreate = vm.Ingredients.Where(i => !string.IsNullOrWhiteSpace(i.RawText)).ToList();

            var rawLines = filteredCreate.Select(i => i.RawText.Trim()).ToList();
            var krogerUpcs = filteredCreate.Select(i => i.SelectedKrogerUpc).ToList();
            var sectionNames = filteredCreate.Select(i => i.Section).ToList();

            var ingredientDtos = await ParseRawLinesToDtos(rawLines, krogerUpcs);

            var request = new CreateRecipeRequest
            {
                Title = normalizedTitle,
                DinnerCategory = string.IsNullOrWhiteSpace(vm.DinnerCategory) ? null : vm.DinnerCategory,
                ImageFile = vm.ImageFile,
                Ingredients = ingredientDtos.Select((d, i) => new CreateRecipeIngredientDto
                {
                    DisplayName = d.DisplayName,
                    Quantity = d.Quantity,
                    MeasurementId = d.MeasurementId,
                    SelectedKrogerUpc = d.KrogerUpc,
                    Section = i < sectionNames.Count ? sectionNames[i] : null
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
            var filteredEdit = vm.Ingredients.Where(i => !string.IsNullOrWhiteSpace(i.RawText)).ToList();

            // Split into modified (need OpenAI parsing) vs unchanged (reuse DB data)
            var modifiedItems = new List<(EditRecipeIngredientVM Item, int Index)>();
            var unchangedIds = new List<int>();
            for (int i = 0; i < filteredEdit.Count; i++)
            {
                var item = filteredEdit[i];
                if (!item.IsModified && item.Id > 0)
                    unchangedIds.Add(item.Id);
                else
                    modifiedItems.Add((item, i));
            }

            // Load unchanged ingredients from DB
            var existingById = unchangedIds.Count > 0
                ? await _context.RecipeIngredients
                    .Where(ri => unchangedIds.Contains(ri.Id))
                    .ToDictionaryAsync(ri => ri.Id)
                : new Dictionary<int, RecipeIngredient>();

            // Only parse modified/new ingredients through OpenAI
            var resultDtos = new EditRecipeIngredientDto[filteredEdit.Count];

            if (modifiedItems.Count > 0)
            {
                var rawLines = modifiedItems.Select(c => c.Item.RawText.Trim()).ToList();
                var krogerUpcs = modifiedItems.Select(c => c.Item.SelectedKrogerUpc).ToList();
                var parsedDtos = await ParseRawLinesToDtos(rawLines, krogerUpcs);

                for (int j = 0; j < modifiedItems.Count; j++)
                {
                    var (item, idx) = modifiedItems[j];
                    var d = parsedDtos[j];
                    resultDtos[idx] = new EditRecipeIngredientDto
                    {
                        Id = item.Id,
                        DisplayName = d.DisplayName,
                        Quantity = d.Quantity,
                        MeasurementId = d.MeasurementId,
                        IngredientId = item.IngredientId,
                        SelectedKrogerUpc = d.KrogerUpc,
                        Section = item.Section
                    };
                }
            }

            // Fill in unchanged ingredients from DB
            for (int i = 0; i < filteredEdit.Count; i++)
            {
                if (resultDtos[i] != null) continue;
                var item = filteredEdit[i];
                var existing = existingById[item.Id];
                resultDtos[i] = new EditRecipeIngredientDto
                {
                    Id = existing.Id,
                    DisplayName = existing.DisplayName,
                    Quantity = existing.Quantity,
                    MeasurementId = existing.MeasurementId,
                    IngredientId = existing.IngredientId,
                    SelectedKrogerUpc = item.SelectedKrogerUpc,
                    Section = item.Section
                };
            }

            var request = new EditRecipeRequest
            {
                Id = vm.RecipeId,
                Title = (vm.Title ?? "").Trim(),
                DinnerCategory = string.IsNullOrWhiteSpace(vm.DinnerCategory) ? null : vm.DinnerCategory,
                ImageFile = vm.ImageFile,
                Ingredients = resultDtos.ToList(),
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
