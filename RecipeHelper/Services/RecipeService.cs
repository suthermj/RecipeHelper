using System.Text.Json;
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
                Instructions = request.Instructions.Count > 0
                    ? JsonSerializer.Serialize(request.Instructions)
                    : null,
                Ingredients = new List<RecipeIngredient>()
            };

            if (request.ImageFile != null && request.ImageFile.Length > 0)
            {
                var blobResponse = await _storageService.StoreRecipeImage(request.ImageFile);
                newRecipe.ImageUri = blobResponse.BlobUri;
            }

            foreach (var ingredient in request.Ingredients)
            {
                var resolved = await ResolveIngredientAsync(ingredient.DisplayName, ingredient.SelectedKrogerUpc);

                newRecipe.Ingredients.Add(new RecipeIngredient
                {
                    DisplayName = ingredient.DisplayName,
                    Quantity = ingredient.Quantity,
                    MeasurementId = ingredient.MeasurementId,
                    SelectedKrogerUpc = ingredient.SelectedKrogerUpc,
                    IngredientId = resolved.Id
                });
            }

            _context.Recipes.Add(newRecipe);
            await _context.SaveChangesAsync();

            return newRecipe;
        }

        /// <summary>
        /// Canonicalizes a display name to find or create an Ingredient record,
        /// ensures the Kroger product exists in DB, and links ingredient ↔ product.
        /// </summary>
        private async Task<Ingredient> ResolveIngredientAsync(string displayName, string? krogerUpc)
        {
            var canonResult = await _ingredientService.CanonicalizeAsync(displayName, CancellationToken.None);
            var ingredient = await _ingredientService.GetIngredientByCanonical(canonResult.CanonicalName);

            if (ingredient == null)
            {
                ingredient = new Ingredient
                {
                    CanonicalName = canonResult.CanonicalName,
                    DefaultDisplayName = displayName
                };
                _context.Ingredients.Add(ingredient);
                await _context.SaveChangesAsync();
            }

            if (!string.IsNullOrEmpty(krogerUpc))
            {
                var productExists = await _context.KrogerProducts.AnyAsync(p => p.Upc == krogerUpc);
                if (!productExists)
                {
                    var krogerProduct = await _krogerService.GetProductDetails(krogerUpc);
                    if (krogerProduct != null)
                    {
                        _context.KrogerProducts.Add(new KrogerProduct
                        {
                            Upc = krogerProduct.upc,
                            Name = krogerProduct.name,
                        });
                        await _context.SaveChangesAsync();
                    }
                }

                var linkExists = await _context.IngredientKrogerProducts
                    .AnyAsync(l => l.IngredientId == ingredient.Id && l.Upc == krogerUpc);

                if (!linkExists)
                {
                    var existingLinks = await _ingredientService.GetLinkedKrogerProductsAsync(ingredient.Id);
                    _context.Add(new IngredientKrogerProduct
                    {
                        IngredientId = ingredient.Id,
                        Upc = krogerUpc,
                        IsDefault = existingLinks.Count == 0
                    });
                }
            }

            return ingredient;
        }

        public async Task<Recipe> UpdateRecipeAsync(EditRecipeRequest request)
        {
            _logger.LogInformation("[UpdateRecipe] Updating recipe {RecipeId}", request.Id);

            var recipe = await _context.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == request.Id);

            if (recipe == null)
            {
                _logger.LogWarning("[UpdateRecipe] Recipe {RecipeId} not found", request.Id);
                return null;
            }

            recipe.Name = request.Title;
            recipe.Instructions = request.Instructions.Count > 0
                ? JsonSerializer.Serialize(request.Instructions)
                : null;
            _logger.LogInformation("[UpdateRecipe] Recipe {RecipeId} title set to [{Title}]", recipe.Id, request.Title);

            if (request.ImageFile != null && request.ImageFile.Length > 0)
            {
                _logger.LogInformation("[UpdateRecipe] Uploading new image [{FileName}] for recipe {RecipeId}", request.ImageFile.FileName, recipe.Id);
                var blobResponse = await _storageService.StoreRecipeImage(request.ImageFile);
                recipe.ImageUri = blobResponse.BlobUri;
            }

            var existingById = recipe.Ingredients.ToDictionary(i => i.Id);
            var incomingIds = request.Ingredients.Where(i => i.Id > 0).Select(i => i.Id).ToHashSet();

            var toRemove = recipe.Ingredients.Where(i => !incomingIds.Contains(i.Id)).ToList();
            foreach (var removed in toRemove)
            {
                _logger.LogInformation("[UpdateRecipe] Removing ingredient {IngredientRowId} [{DisplayName}] from recipe {RecipeId}", removed.Id, removed.DisplayName, recipe.Id);
            }
            _context.RecipeIngredients.RemoveRange(toRemove);

            foreach (var dto in request.Ingredients)
            {
                if (dto.Id > 0 && existingById.TryGetValue(dto.Id, out var existing))
                {
                    _logger.LogInformation("[UpdateRecipe] Updating existing ingredient {IngredientRowId} [{DisplayName}]", existing.Id, dto.DisplayName);
                    existing.DisplayName = dto.DisplayName;
                    existing.Quantity = dto.Quantity;
                    existing.MeasurementId = dto.MeasurementId;
                    existing.SelectedKrogerUpc = dto.SelectedKrogerUpc;
                    existing.IngredientId = dto.IngredientId;
                }
                else
                {
                    _logger.LogInformation("[UpdateRecipe] Adding new ingredient [{DisplayName}] to recipe {RecipeId}", dto.DisplayName, recipe.Id);
                    var resolved = await ResolveIngredientAsync(dto.DisplayName, dto.SelectedKrogerUpc);

                    recipe.Ingredients.Add(new RecipeIngredient
                    {
                        RecipeId = recipe.Id,
                        DisplayName = dto.DisplayName,
                        Quantity = dto.Quantity,
                        MeasurementId = dto.MeasurementId,
                        SelectedKrogerUpc = dto.SelectedKrogerUpc,
                        IngredientId = resolved.Id
                    });
                }
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("[UpdateRecipe] Recipe {RecipeId} saved successfully", recipe.Id);
            return recipe;
        }
    }
}
