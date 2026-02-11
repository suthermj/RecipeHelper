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
            ViewBag.Measurements = _context.Measurements
                    .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Name })
                    .ToList();

            if (id == null)
            {
                return View("Create", new CreateRecipeVM2());
            }
            else
            {
                var recipe = await _context.Recipes.Where(r => r.Id == id).Select(r => new EditRecipeVM
                {
                    RecipeId = r.Id,
                    Title = r.Name,
                    ImageUri = r.ImageUri,
                    Ingredients = r.Ingredients.Select(rp => new EditRecipeIngredientVM
                    {
                        Id = rp.Id,
                        DisplayName = rp.DisplayName,
                        Quantity = rp.Quantity,
                        MeasurementId = rp.MeasurementId,
                        SelectedKrogerUpc = rp.SelectedKrogerUpc,
                        IngredientId = rp.IngredientId
                    }).ToList(),
                }).FirstOrDefaultAsync();

                return View("Edit", recipe);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditRecipe(EditRecipeVM vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Measurements = _context.Measurements
                    .Select(m => new SelectListItem { Value = m.Id.ToString(), Text = m.Name })
                    .ToList();

                return View("Edit", vm);
            }

            var update = await _recipeService.UpdateRecipeAsync(vm.ToRequest());

            return RedirectToAction("ViewRecipe", new { Id = vm.RecipeId });

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
