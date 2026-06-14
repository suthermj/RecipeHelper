using System.Text.Json;
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
        private readonly StorageService _storageService;
        private readonly IngredientsService _ingredientsService;

        public ImportController(ImportService importService, ILogger<ImportController> logger, SpoonacularService spoonacularService, RecipeService recipeService, MeasurementService measurementService, StorageService storageService, IngredientsService ingredientsService)
        {
            _logger = logger;
            _spoonacularService = spoonacularService;
            _importService = importService;
            _recipeService = recipeService;
            _measurementService = measurementService;
            _storageService = storageService;
            _ingredientsService = ingredientsService;
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
                    var backupImport = await _spoonacularService.Import(model.Url);

                    if (backupImport is null)
                    {
                        TempData["ErrorMessage"] = "Could not extract recipe from the provided URL.";
                        return View(model);
                    }

                    model.Preview = backupImport;
                    return View(model);
                }

                 model.Preview = ImportRecipeVM.FromSpoonacular(importedRecipe);

                 return View(model);
                
            }

            return View(model);
        }

        // Returns MappedImportRecipeVm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportRecipeFromPhoto(PhotoImportPageVM vm)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var photoCount = vm.Photos?.Count ?? 0;
            _logger.LogInformation(
                "Photo import started. PhotoCount={PhotoCount}, UsePhotoAsImage={UsePhotoAsImage}",
                photoCount,
                vm.UsePhotoAsImage);

            if (vm.Photos is not null)
            {
                for (var i = 0; i < vm.Photos.Count; i++)
                {
                    var photo = vm.Photos[i];
                    _logger.LogInformation(
                        "Photo import file received. Index={Index}, FileName={FileName}, ContentType={ContentType}, LengthBytes={LengthBytes}",
                        i,
                        photo.FileName,
                        photo.ContentType,
                        photo.Length);
                }
            }

            try
            {
                var normalizedPhotos = await _ingredientsService.NormalizeRecipePhotosAsync(vm.Photos ?? new());
                var preview = await _ingredientsService.ExtractRecipeFromNormalizedPhotosAsync(normalizedPhotos);
                _logger.LogInformation(
                    "Photo import extraction completed. ElapsedMs={ElapsedMs}, TitleLength={TitleLength}, IngredientCount={IngredientCount}, StepCount={StepCount}",
                    stopwatch.ElapsedMilliseconds,
                    preview.Title?.Length ?? 0,
                    preview.Ingredients?.Count ?? 0,
                    preview.Steps?.Count ?? 0);

                if (vm.UsePhotoAsImage && vm.Photos?.Count > 0)
                {
                    try
                    {
                        var uploadStopwatch = System.Diagnostics.Stopwatch.StartNew();
                        var coverPhoto = normalizedPhotos[0];
                        await using var coverStream = new MemoryStream(coverPhoto.Bytes);
                        var blob = await _storageService.StoreRecipeImage(coverStream, coverPhoto.FileName, coverPhoto.MimeType);
                        if (blob is null)
                        {
                            _logger.LogWarning(
                                "Photo import cover upload returned no blob. FileName={FileName}, WasConverted={WasConverted}",
                                coverPhoto.FileName,
                                coverPhoto.WasConverted);
                            return View("ImportRecipe", new ImportRecipePageVM { Preview = preview });
                        }

                        preview.Image = blob.BlobUri;
                        _logger.LogInformation(
                            "Photo import cover upload completed. ElapsedMs={ElapsedMs}, BlobName={BlobName}, WasConverted={WasConverted}",
                            uploadStopwatch.ElapsedMilliseconds,
                            blob.BlobName,
                            coverPhoto.WasConverted);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to upload recipe photo to blob storage");
                    }
                }

                _logger.LogInformation("Photo import request completed. ElapsedMs={ElapsedMs}", stopwatch.ElapsedMilliseconds);
                return View("ImportRecipe", new ImportRecipePageVM { Preview = preview });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Photo import validation failed. ElapsedMs={ElapsedMs}", stopwatch.ElapsedMilliseconds);
                return View("ImportRecipe", new ImportRecipePageVM { Error = ex.Message, PreferredTab = "photo" });
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "Photo import extraction timed out. ElapsedMs={ElapsedMs}", stopwatch.ElapsedMilliseconds);
                return View("ImportRecipe", new ImportRecipePageVM
                {
                    Error = "Recipe extraction is taking longer than expected. Try one clear photo at a time, or retake the photo closer to the recipe text.",
                    PreferredTab = "photo"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Photo import extraction failed. ElapsedMs={ElapsedMs}", stopwatch.ElapsedMilliseconds);
                return View("ImportRecipe", new ImportRecipePageVM
                {
                    Error = "Could not extract the recipe from the photo. Try a clearer image or enter the recipe manually.",
                    PreferredTab = "photo"
                });
            }
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
            mappedImportRecipeVm.SourceUrl = vm.SourceUrl;
            mappedImportRecipeVm.Steps = vm.Steps ?? new();
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
                vm.AvailableMeasurements = await _measurementService.GetAllMeasurementsAsync()
                .ContinueWith(t => t.Result.Select(m => new SelectListItem
                {
                    Value = m.Name,
                    Text = m.Name
                }));
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
