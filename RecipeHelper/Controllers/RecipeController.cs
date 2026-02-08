using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models;
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

        public RecipeController(ILogger<RecipeController> logger, DatabaseContext context, StorageService storageService, SpoonacularService spoonacularService, RecipeService recipeService)
        {
            _logger = logger;
            _context = context;
            _storageService = storageService;
            _spoonacularService = spoonacularService;
            _recipeService = recipeService;
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
            var recipe = _context.Recipes.Where(r => r.Id == Id).Select(r => new ViewRecipeVM
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
            }).FirstOrDefault();

            return View(recipe);
        }

        public IActionResult SaveRecipe(ViewRecipeVM model)
        {
            return RedirectToAction("Recipe");
        }

        // Returns create recipe view or shows current recipe if id is not null
        // VM Returned: CreateRecipeVM2
        [HttpGet]
        public async Task<ActionResult> CreateEditRecipe(int? id)
        {
            if (id == null)
            {
                ViewBag.Measurements = _context.Measurements
                    .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Name })
                    .ToList();
                return View("Create", new CreateRecipeVM2());
            }
            else
            {
                var recipe = await _context.Recipes.Where(r => r.Id == id).Select(r => new CreateRecipeVM
                {
                    recipeId = r.Id,
                    recipeName = r.Name,
                    imageUri = r.ImageUri,
                    ingredients = r.Ingredients.Select(rp => new IngredientVM
                    {
                        Id = rp.Id,
                        Quantity = rp.Quantity,
                    }).ToList(),
                }).FirstOrDefaultAsync();

                recipe.modifying = true;

                return View(recipe);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateRecipe(CreateRecipeVM2 vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Measurements = _context.Measurements
                    .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Name })
                    .ToList();
                
                return View("Create", vm);
            }

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

            if (recipeExists) return View("Create", vm);
            var recipe = await _recipeService.CreateRecipe(vm.ToRequest());

            return RedirectToAction("ViewRecipe", new { Id = recipe.Id });

        }

        public async Task<ActionResult> CreateEditRecipeForm(CreateRecipeVM newRecipe)
        {
            var recipeExists = await _context.Recipes.Where(r => r.Name.Equals(newRecipe.recipeName)).FirstOrDefaultAsync();

            if (recipeExists != null && !newRecipe.modifying)
            {
                _logger.LogInformation($"Recipe name [{newRecipe.recipeName}] already exists");
                TempData["ErrorMessage"] = "Please correct the errors before proceeding.";
                return RedirectToAction("CreateEditRecipe");
            }

            var allProducts = _context.Products.Select(p => new ProductVM
            {
                Name = p.Name,
                Upc = p.Upc,
                Id = p.Id
            }).ToList();

            var availableMeasurements = _context.Measurements.Select(m => new SelectListItem
            {
                Value = m.Id.ToString(),
                Text = m.Name
            }).ToList();

            if (newRecipe.modifying)
            {
                var publishedRecipe = _context.Recipes
                    .Include(r => r.Ingredients)
                    .ThenInclude(rp => rp.SelectedKrogerProduct)
                    .FirstOrDefault(r => r.Id == newRecipe.recipeId);

                var currentIngredients = publishedRecipe.Ingredients.Select(rp => new ProductVM
                {
                    Id = rp.IngredientId,
                    Name = rp.DisplayName,
                    Upc = rp.SelectedKrogerUpc,
                    Quantity = rp.Quantity,
                    MeasurementId = rp.MeasurementId
                }).ToList();

                var currentIngredientIds = publishedRecipe.Ingredients.Select(rp => rp.IngredientId).ToHashSet();

                var filteredAllProducts = allProducts.Where(p => !currentIngredientIds.Contains(p.Id)).ToList();

                var draftRecipe = new DraftRecipe();
                draftRecipe.Name = newRecipe.recipeName;
                draftRecipe.PublishedRecipeId = publishedRecipe.Id;
                draftRecipe.ImageUri = publishedRecipe.ImageUri;

                // Detects new recipe image
                if (newRecipe.imageFile != null)
                {
                    StoreImageBlobResponse newImageBlobResponse = new();
                    if (newRecipe.imageFile != null)
                    {
                        _logger.LogInformation($"storing recipe image in blob storage [{newRecipe.imageFile.FileName}]");
                        newImageBlobResponse = await _storageService.StoreRecipeImage(newRecipe.imageFile);
                    }
                    draftRecipe.ImageUri = newImageBlobResponse.BlobUri;
                }

                try
                {
                    _context.DraftRecipes.Add(draftRecipe);
                    _context.SaveChanges();
                }
                catch (Exception ex)
                {
                    return BadRequest(ex.Message);
                }

                ModifyIngredientsVM vm = new ModifyIngredientsVM
                {
                    publishedRecipeId = publishedRecipe.Id,
                    RecipeId = draftRecipe.Id,
                    CurrentIngredients = currentIngredients,
                    AllProducts = filteredAllProducts,
                    AvailableMeasurements = availableMeasurements
                };

                return View("ModifyIngredients", vm);

            }
            else
            {
                StoreImageBlobResponse blobResponse = new();
                if (newRecipe.imageFile != null)
                {
                    _logger.LogInformation($"storing recipe image in blob storage [{newRecipe.imageFile.FileName}]");
                    blobResponse = await _storageService.StoreRecipeImage(newRecipe.imageFile);
                }

                var recipe = new Recipe
                {
                    Name = newRecipe.recipeName,
                    ImageUri = blobResponse.BlobUri,
                };

                try
                {
                    _context.Recipes.Add(recipe);
                    _context.SaveChanges();
                }
                catch (Exception ex)
                {
                    return BadRequest(ex.Message);
                }

                var vm = new IngredientsVM
                {
                    RecipeId = recipe.Id,
                    RecipeName = recipe.Name,
                    ImageUri = recipe.ImageUri,
                    Ingredients = allProducts,
                    AvailableMeasurements = availableMeasurements
                };

                return View("ProductToChoose", vm);
            }
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
