using Microsoft.AspNetCore.Mvc;
using RecipeHelper.Services;
using RecipeHelper.Utility;
using RecipeHelper.ViewModels;

namespace RecipeHelper.Controllers
{
    public class ImportController : Controller
    {
        private ImportService _importService;
        private ILogger<ImportController> _logger;
        private readonly SpoonacularService _spoonacularService;

        public ImportController(ImportService importService, ILogger<ImportController> logger, SpoonacularService spoonacularService)
        {
            _logger = logger;
            _spoonacularService = spoonacularService;
            _importService = importService;
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
            return View("MappedImportedRecipe", recipePreview.ToVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveImportedRecipe(ConfirmMappingVM vm)
        {
            if (string.IsNullOrWhiteSpace(vm.Title) || vm.Ingredients.Count == 0)
            {
                _logger.LogWarning("Invalid imported recipe data: missing title or ingredients.");
                TempData["ErrorMessage"] = "Missing title or ingredients.";
                return RedirectToAction(nameof(ImportRecipe));
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
