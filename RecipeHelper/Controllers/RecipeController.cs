using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models;
using RecipeHelper.Services;
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
                Ingredients = r.RecipeProducts.Select(rp => new IngredientVM
                {
                    Name = rp.Product.Name,
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
                Ingredients = r.RecipeProducts.Select(rp => new IngredientVM
                {
                    Name = rp.Product.Name,
                    Quantity = rp.Quantity,
                    Measurement = rp.Measurement.Name,
                }).ToList(),
            }).FirstOrDefault();

            return View(recipe);
        }

        


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveModifiedIngredients(ModifyIngredientsVM model)
        {
            var currentIngredientsChoices = model.CurrentIngredients.Where(i => i.Quantity != 0);
            var allIngredientChoices = model.AllProducts.Where(i => i.Quantity != 0);

            var allSelections = currentIngredientsChoices.Concat(allIngredientChoices).ToList();

            var publishedRecipe =  _context.Recipes.Include(r => r.RecipeProducts)
                    .ThenInclude(rp => rp.Product)
                    .FirstOrDefault(r => r.Id == model.publishedRecipeId);

            var draftRecipe = await _context.DraftRecipes.FindAsync(model.RecipeId);

            // update published recipe name / image uri if modified
            if (!publishedRecipe.Name.Equals(draftRecipe.Name))
            {
                publishedRecipe.Name = draftRecipe.Name;
            }

            if (publishedRecipe.ImageUri != draftRecipe.ImageUri)
            {
                publishedRecipe.ImageUri = draftRecipe.ImageUri;
            }

            try
            {
                var finalRecipe = _context.Recipes.Update(publishedRecipe);

                // delete draft
                _context.DraftRecipes.Remove(draftRecipe);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving recipe ingredient");
            }

            // Delete current ingredients
            try
            {
                var publishedRecipeProducts = _context.RecipeProducts.Where(r => r.RecipeId == model.publishedRecipeId);
                _context.RecipeProducts.RemoveRange(publishedRecipeProducts);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving recipe ingredient");
            }

            // update to new set of ingredients
            foreach (var ingredient in allSelections)
            {
                var recipeProduct = new RecipeProduct
                {
                    RecipeId = model.publishedRecipeId,
                    ProductId = ingredient.Id,
                    Quantity = ingredient.Quantity,
                    MeasurementId = ingredient.MeasurementId
                };
                _context.RecipeProducts.Add(recipeProduct);
            }

            try
            {
               await  _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving recipe ingredient");
            }

            var recipeReview = _context.Recipes.Where(r => r.Id == model.publishedRecipeId).Select(r => new ViewRecipeVM
            {
                Id = r.Id,
                RecipeName = r.Name,
                ImageUri = r.ImageUri,
                Ingredients = r.RecipeProducts.Select(rp => new IngredientVM
                {
                    Name = rp.Product.Name,
                    Quantity = rp.Quantity,
                    Id = rp.Product.Id,
                    Measurement = rp.Measurement.Name
                }).ToList(),
            }).FirstOrDefault();

            return View("ReviewRecipe", recipeReview);
        }

        public IActionResult SaveRecipe(ViewRecipeVM model)
        {
            return RedirectToAction("Recipe");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveIngredients(IngredientsVM model)
        {
            var ingredients = model.Ingredients;

            var chosenIngredients = ingredients.Where(i => i.Quantity > 0);

            foreach (var ingredient in chosenIngredients)
            {
                if (ingredient.MeasurementId == 0) { ingredient.MeasurementId = null; }
                var recipeProduct = new RecipeProduct
                {
                    RecipeId = model.RecipeId,
                    ProductId = ingredient.Id,
                    Quantity = ingredient.Quantity,
                    MeasurementId = ingredient.MeasurementId
                };
                _context.RecipeProducts.Add(recipeProduct);
            }
            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving recipe ingredients");
            }

            var recipeToReview = _context.Recipes.Where(r => r.Id == model.RecipeId).Select(r => new ViewRecipeVM
            {
                Id = r.Id,
                RecipeName = r.Name,
                ImageUri = r.ImageUri,
                Ingredients = r.RecipeProducts.Select(rp => new IngredientVM
                {
                    Id = rp.ProductId,
                    Name = rp.Product.Name,
                    Quantity = rp.Quantity,
                    Measurement = rp.Measurement.Name,
                }).ToList(),
            }).FirstOrDefault();
            return View("ReviewRecipe", recipeToReview);
        }

        public async Task<ActionResult> CreateEditRecipe(int? id)
        {
            if (id == null)
            {
                return View(new CreateRecipeVM());
            }
            else
            {
                var recipe = await _context.Recipes.Where(r => r.Id == id).Select(r => new CreateRecipeVM
                {
                    recipeId = r.Id,
                    recipeName = r.Name,
                    imageUri = r.ImageUri,
                    ingredients = r.RecipeProducts.Select(rp => new IngredientVM
                    {
                        Id = rp.Id,
                        Quantity = rp.Quantity,
                    }).ToList(),
                }).FirstOrDefaultAsync();

                recipe.modifying = true;

                return View(recipe);
            }
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
                    .Include(r => r.RecipeProducts)
                    .ThenInclude(rp => rp.Product)
                    .FirstOrDefault(r => r.Id == newRecipe.recipeId);

                var currentIngredients = publishedRecipe.RecipeProducts.Select(rp => new ProductVM
                {
                    Id = rp.ProductId,
                    Name = rp.Product.Name,
                    Upc = rp.Product.Upc,
                    Quantity = rp.Quantity,
                    MeasurementId = rp.MeasurementId
                }).ToList();

                var currentIngredientIds = publishedRecipe.RecipeProducts.Select(rp => rp.ProductId).ToHashSet();

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
                    string fileName = splitImageUri[splitImageUri.Length -1];
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

        

        /*
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmImportedMapping(ConfirmMappingVM vm)
        {
            var recipeId = await _recipeService.SaveImportedRecipe(vm);

            TempData["SuccessMessage"] = "Recipe imported successfully.";
            return RedirectToAction(nameof(ImportSuccess), new { recipeId });
        }

        [HttpGet]
        public async Task<IActionResult> ImportSuccess(int recipeId)
        {
            // fetch a lightweight summary for display
            var summary = await _recipeService.GetImportSummary(recipeId);
            return View(summary);
        }*/
    }
}
