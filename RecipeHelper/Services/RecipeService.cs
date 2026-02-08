using RecipeHelper.Models.RecipeModels;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models;
using RecipeHelper.Models.IngredientModels;
using RecipeHelper.Models.Kroger;

namespace RecipeHelper.Services
{
    public class RecipeService
    {
        private readonly ILogger<RecipeService> _logger;
        private DatabaseContext _context;
        private KrogerService _krogerService;
        private StorageService _storageService;
        private IngredientsService _ingredientService;
        public RecipeService(ILogger<RecipeService> logger, DatabaseContext context, KrogerService krogerService, StorageService storageService, IngredientsService ingredientService)
        {
            _context = context;
            _logger = logger;
            _krogerService = krogerService;
            _storageService = storageService;
            _ingredientService = ingredientService;
        }

        public async Task<bool> RecipeNameExists(string recipeName)
        {
            var exists = await _context.Recipes.Where(r => r.Name.ToLower().Equals(recipeName.ToLower())).FirstOrDefaultAsync();

            if (exists == null) return false;
            return true;
        }

        public async Task<Recipe> CreateRecipe(CreateRecipeRequest request)
        {
            var newRecipe = new Recipe
            {
                Name = request.Title,
                Ingredients = new List<RecipeIngredient>()
            };

            if (request.ImageFile != null && request.ImageFile.Length > 0)
            {
                var blobResponse = await _storageService.StoreRecipeImage(request.ImageFile);
                newRecipe.ImageUri = blobResponse.BlobUri;
            }

            var currentProducts = await _context.KrogerProducts.Select(p => new KrogerProduct
            {
                Upc = p.Upc,
                Name = p.Name
            }).AsNoTracking()
            .ToListAsync();

            // Process each ingredient
            foreach (var ingredient in request.Ingredients)
            {
                var conicalResult = await _ingredientService.CanonicalizeAsync(ingredient.DisplayName, CancellationToken.None);
                var canonicalIngredient = await _ingredientService.GetIngredientByCanonical(conicalResult.CanonicalName);

                // selected kroger product does not exist in db, fetch and add it
                if (ingredient.SelectedKrogerUpc != null && !currentProducts.Where(p => p.Upc == ingredient.SelectedKrogerUpc).Any())
                {
                    var krogerProduct = await _krogerService.GetProductDetails(ingredient.SelectedKrogerUpc);
                    if (krogerProduct != null)
                    {
                        // Add the new product to the database
                        var newKrogerProduct = new KrogerProduct
                        {
                            Upc = krogerProduct.upc,
                            Name = krogerProduct.name,
                        };
                        _context.KrogerProducts.Add(newKrogerProduct);
                        await _context.SaveChangesAsync();
                    }
                }

                if (canonicalIngredient == null)
                {
                    // Create new Ingredient entry
                    var newIngredient = new Ingredient
                    {
                        CanonicalName = conicalResult.CanonicalName,
                        DefaultDisplayName = ingredient.DisplayName
                    };
                    _context.Ingredients.Add(newIngredient);
                    await _context.SaveChangesAsync();

                    var recipeIngredient = new RecipeIngredient
                    {
                        DisplayName = ingredient.DisplayName,
                        Quantity = ingredient.Quantity,
                        MeasurementId = ingredient.MeasurementId,
                        SelectedKrogerUpc = ingredient.SelectedKrogerUpc,
                        IngredientId = newIngredient.Id
                    };

                    if (!String.IsNullOrEmpty(ingredient.SelectedKrogerUpc))
                    {
                        IngredientKrogerProduct krogerProductLink = new IngredientKrogerProduct
                        {
                            IngredientId = newIngredient.Id,
                            Upc = ingredient.SelectedKrogerUpc,
                            IsDefault = true
                        };

                        _context.Add(krogerProductLink);
                    }
                    
                    newRecipe.Ingredients.Add(recipeIngredient);
                }
                else
                {
                    var recipeIngredient = new RecipeIngredient
                    {
                        DisplayName = ingredient.DisplayName,
                        Quantity = ingredient.Quantity,
                        MeasurementId = ingredient.MeasurementId,
                        SelectedKrogerUpc = ingredient.SelectedKrogerUpc,
                        IngredientId = canonicalIngredient.Id
                    };

                    if (!String.IsNullOrEmpty(ingredient.SelectedKrogerUpc))
                    {
                        var ingredientHasExistingMapping = await _ingredientService.GetLinkedKrogerProductsAsync(canonicalIngredient.Id);

                        IngredientKrogerProduct krogerProductLink = new IngredientKrogerProduct
                        {
                            IngredientId = canonicalIngredient.Id,
                            Upc = ingredient.SelectedKrogerUpc,
                            IsDefault = ingredientHasExistingMapping.Count != 0 ? false : true
                        };

                        _context.Add(krogerProductLink);

                    }

                    newRecipe.Ingredients.Add(recipeIngredient);
                }

            }
            // Save the new recipe to the database
            _context.Recipes.Add(newRecipe);
            await _context.SaveChangesAsync();

            return newRecipe;
        }

        
    }
}

