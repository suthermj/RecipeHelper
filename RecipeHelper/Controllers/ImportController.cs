using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;
using RecipeHelper.Models.Import;
using RecipeHelper.Services;
using RecipeHelper.Utility;
using RecipeHelper.ViewModels;

namespace RecipeHelper.Controllers
{
    public class ImportController : Controller
    {
        private ImportService _importService;
        private RecipeService _recipeService;
        private readonly MeasurementService _measurementService;
        private ILogger<ImportController> _logger;
        private readonly SpoonacularService _spoonacularService;

        public ImportController(ImportService importService, ILogger<ImportController> logger, SpoonacularService spoonacularService, RecipeService recipeService, MeasurementService measurementService)
        {
            _logger = logger;
            _spoonacularService = spoonacularService;
            _importService = importService;
            _recipeService = recipeService;
            _measurementService = measurementService;
        }

        [HttpGet]
        public async Task<ActionResult> ImportRecipe(ImportRecipePageVM model)
        {
            if (model is null)
            {
                return View(new ImportRecipePageVM());
            }

            if (!string.IsNullOrWhiteSpace(model.Url))
            {
                _logger.LogInformation("Importing recipe from URL: {Url}", model.Url);
                var importedRecipe = await _spoonacularService.ImportRecipe(model.Url);
                if (importedRecipe is null)
                {
                    TempData["ErrorMessage"] = "Could not extract recipe from the provided URL.";
                    return View(model);
                }

                model.Preview = ImportRecipeVM.FromSpoonacular(importedRecipe);

                return View(model);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GetImportedRecipePreview(PreviewImportedRecipeVM vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Title) || vm.Ingredients.Count == 0)
            {
                _logger.LogWarning("Invalid imported recipe data: missing title or ingredients.");
                TempData["ErrorMessage"] = "Missing title or ingredients.";
                return RedirectToAction(nameof(ImportRecipe));
            }


            var recipePreview = await _importService.GetImportedRecipePreview(vm.ToRequest());

            var mappedImportRecipeVm = recipePreview.ToVm();
            mappedImportRecipeVm.AvailableMeasurements = await _measurementService.GetAllMeasurementsAsync()
                .ContinueWith(t => t.Result.Select(m => new SelectListItem
                {
                    Value = m.Name,
                    Text = m.Name
                }));
            ModelState.Clear();
            return View("MappedImportedRecipe", mappedImportRecipeVm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveImportedRecipe(MappedImportedRecipeVM vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Title) || vm.Ingredients.Count == 0)
            {
                _logger.LogWarning("Invalid imported recipe data: missing title or ingredients.");
                TempData["ErrorMessage"] = "Missing title or ingredients.";
                return RedirectToAction(nameof(ImportRecipe));
            }

            if (await _recipeService.RecipeNameExists(vm.Title))
            {
                _logger.LogInformation("Recipe name {recipeName} already exists", vm.Title);
                ModelState.AddModelError(nameof(vm.Title), $"Recipe name \"{vm.Title}\" already exists.");
                return View("MappedImportedRecipe", vm);
            }
            
            var result = await _importService.SaveImportedRecipe(vm.ToRequest());

            if (!result.Success)
            {
                TempData["ErrorMessage"] = result.ErrorMessage ?? "Could not import recipe.";
                return RedirectToAction(nameof(ImportRecipe));
            }

            return RedirectToAction("ViewRecipe", "Recipe", new { Id = result.RecipeId });
        }
    }
}
