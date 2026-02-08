using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NuGet.Protocol;
using RecipeHelper.Models;
using RecipeHelper.Models.Import;
using RecipeHelper.Models.IngredientModels;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Utility;
using RecipeHelper.ViewModels;

namespace RecipeHelper.Services
{
    public class ImportService
    {
        private readonly DatabaseContext _context;
        private readonly ILogger<ImportService> _logger;
        private  IngredientsService _ingredientService;
        private readonly KrogerService _krogerService;
        private readonly ProductService _productService;
        public ImportService(DatabaseContext context, ILogger<ImportService> logger, KrogerService krogerService, IngredientsService ingredientService, ProductService productService)
        {
            _context = context;
            _logger = logger;
            _krogerService = krogerService;
            _ingredientService = ingredientService;
            _productService = productService;
        }
        public async Task<ImportPreview> GetImportedRecipePreview(PreviewImportedRecipeRequest importedRecipe)
        {
            var title = importedRecipe.Title;
            var imageUri = importedRecipe.Image;
            var ingredients = importedRecipe.Ingredients;
            if (title is null)
            {
                _logger.LogError("Recipe title is null");
                return null;
            }

            ImportPreview importPreview = new ImportPreview
            {
                Title = title,
                Image = imageUri
            };

            var ingredientList = new List<ImportPreviewIngredient>();


            var ingredientDict = await _context.Ingredients.AsNoTracking()
                .Select(i => new { i.Id, i.CanonicalName })
                .ToDictionaryAsync(x => x.CanonicalName, x => x.Id);

            // merge any duplicates
            var mergedIngredients = ingredients
                        .GroupBy(i => new
                        {
                            Name = i.Name,
                            CleanName = i.CleanName.Trim().ToLowerInvariant(),
                            Unit = MeasurementHelper.NormalizeMeasurementUnit(i.Unit) ?? i.Unit
                        })
                        .Select(g => new PreviewImportedRecipeIngredient
                        {
                            Name = g.Key.Name,
                            CleanName = g.Key.CleanName,
                            Unit = g.Key.Unit,
                            Amount = g.Sum(x => x.Amount)
                        })
                        .ToList();

            var availableMeasurements = _context.Measurements.Select(m => new SelectListItem
            {
                Value = m.Id.ToString(),
                Text = m.Name
            }).ToList();

            var ingredientsNoExactMatch = new List<PreviewImportedRecipeIngredient>();

            foreach (var ingredient in mergedIngredients)
            {
                var ingredientPreview = new ImportPreviewIngredient
                {
                    Name = ingredient.Name,
                    Unit = ingredient.Unit,
                    Amount = ingredient.Amount,
                };

                var canonicalName = await _ingredientService.CanonicalizeAsync(ingredient.CleanName, CancellationToken.None);
                var cleanedName = canonicalName.CanonicalName;  

                if (ingredientDict.TryGetValue(cleanedName, out var ingredientId))
                {
                    ingredientPreview.MatchedIngredientId = ingredientId;
                    ingredientPreview.MatchedCanonicalName = cleanedName;

                    // Get default kroger mapping for this ingredient (IngredientKrogerProduct)
                    var defaultMap = await _context.IngredientKrogerProducts
                        .AsNoTracking()
                        .Where(m => m.IngredientId == ingredientId && m.IsDefault)
                        .Select(m => new { m.Upc, ProductName = m.KrogerProduct.Name })
                        .FirstOrDefaultAsync();

                    if (defaultMap != null)
                    {
                        ingredientPreview.SuggestedProductUpc = defaultMap.Upc;
                        ingredientPreview.SuggestedProductName = defaultMap.ProductName;
                    }

                    ingredientList.Add(ingredientPreview);
                }
                else
                {
                    ingredient.CleanName = cleanedName;
                    ingredientsNoExactMatch.Add(ingredient);
                }
            }

            var fuzzySearchIngredients = await IngredientFuzzySearch(ingredientsNoExactMatch, ingredientDict);
            ingredientList.AddRange(fuzzySearchIngredients);
            importPreview.Ingredients = ingredientList;
            return importPreview;
        }


        public async Task<ImportRecipeResponse> SaveImportedRecipe(ImportRecipeRequest recipe)
        {
            ImportRecipeResponse response = new ImportRecipeResponse();
            await using var tx = await _context.Database.BeginTransactionAsync();

            var newRecipe = new Recipe
            {
                Name = recipe.Title,
                ImageUri = recipe.Image,
                Ingredients = new List<RecipeIngredient>()
            };

            // Build a case-insensitive lookup for measurements once
            var measurementDict = await _context.Measurements
                .AsNoTracking()
                .ToDictionaryAsync(m => m.Name, m => m.Id, StringComparer.OrdinalIgnoreCase);

            if (recipe.Ingredients.IsNullOrEmpty())
            {
                _logger.LogWarning("No ingredients found in the imported recipe.");
                response.ErrorMessage = "No ingredients found in the imported recipe.";
                return response;
            }
            _logger.LogInformation($"Attempting to add [{recipe.Ingredients.Count}] ingredients to recipe [{recipe.Title}]");

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.Include == false) continue;

                // Create new canonical ingredient since no ingredientId is present
                if (ingredient.IngredientId is null || ingredient.IngredientId == 0)
                {
                    var canonicalResult = await _ingredientService.CanonicalizeAsync(ingredient.Name, CancellationToken.None);
                    var canonicalName = canonicalResult.CanonicalName;

                    var canonicalIngredient = await _ingredientService.GetIngredientByCanonical(canonicalName);

                    if (canonicalIngredient == null)
                    {
                        _logger.LogInformation($"Creating new ingredient [{canonicalName}]");
                        canonicalIngredient = new Ingredient
                        {
                            CanonicalName = canonicalName,
                        };
                        await _context.Ingredients.AddAsync(canonicalIngredient);
                        await _context.SaveChangesAsync();
                        ingredient.IngredientId = canonicalIngredient.Id;
                    }
                    else
                    {
                        ingredient.IngredientId = canonicalIngredient.Id;
                    }
                }

                var normalizedMeasurementUnit = MeasurementHelper.NormalizeMeasurementUnit(ingredient.Unit);

                var recipeIngredient = new RecipeIngredient
                {
                    DisplayName = ingredient.Name,
                    Quantity = ingredient.Amount,
                    MeasurementId = measurementDict.TryGetValue(normalizedMeasurementUnit, out var id) ? id : (int?)null,
                    SelectedKrogerUpc = ingredient.Upc,
                    IngredientId = (int)ingredient.IngredientId
                };

                // Link kroger product to ingredient if UPC is provided 
                // Create Kroger product if not already in database
                if (!String.IsNullOrEmpty(ingredient.Upc)) 
                { 
                    // check if kroger product already exists
                    var krogerProduct = await _productService.GetProductAsync(ingredient.Upc);

                    // add kroger product if not found
                    if (krogerProduct == null)
                    {
                        _logger.LogInformation($"Looking up Kroger product [{ingredient.Name}] [{ingredient.Upc}] to add to database");
                        var productDetails = await _krogerService.GetProductDetails(ingredient.Upc);

                        if (productDetails != null)
                        {
                            productDetails.RemoveKrogerBrandFromName();
                            krogerProduct = new KrogerProduct
                            {
                                Name = productDetails.name ?? ingredient.Name,
                                Upc = productDetails.upc ?? ingredient.Upc,
                                Price = (decimal)(productDetails?.regularPrice ?? 0)
                            };
                            await _context.KrogerProducts.AddAsync(krogerProduct);
                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"Added [{krogerProduct.Name}] to database.");
                        }
                        
                    }

                    var mappingExists = await _ingredientService.IngredientProductLinkExists((int)ingredient.IngredientId, ingredient.Upc);

                    if (!mappingExists)
                    {
                        var hasExistingKrogerMappings = await _ingredientService.GetLinkedKrogerProductsAsync((int)ingredient.IngredientId);

                        IngredientKrogerProduct krogerProductLink = new IngredientKrogerProduct
                        {
                            IngredientId = (int)ingredient.IngredientId,
                            Upc = ingredient.Upc,
                            IsDefault = hasExistingKrogerMappings.Count != 0 ? false : true
                        };

                        _context.Add(krogerProductLink);
                    }
                }

                newRecipe.Ingredients.Add(recipeIngredient);
            }

            _context.Recipes.Add(newRecipe);

            try
            {
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                _logger.LogInformation($"Created new recipe [{newRecipe.Name}]. Id [{newRecipe.Id}]");
                response.RecipeId = newRecipe.Id;
                response.Success = true;
                return response;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error saving recipe");
                response.ErrorMessage = "Error saving recipe: " + ex.Message;
                return response;
            }
        }

        // Lightweight Levenshtein ratio (0..1)
        private static double LevenshteinRatio(string s, string t)
        {
            if (s == t) return 1.0;
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(t)) return 0.0;

            var n = s.Length; var m = t.Length;
            var d = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= m; j++)
                {
                    int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            int dist = d[n, m];
            int maxLen = Math.Max(n, m);
            return 1.0 - (double)dist / maxLen;
        }


        private async Task<List<ImportPreviewIngredient>> IngredientFuzzySearch(List<PreviewImportedRecipeIngredient> ingredients, Dictionary<string, int> allIngredients)
        {
            var ingredientList = new List<ImportPreviewIngredient>();
            var throttle = new SemaphoreSlim(5);

            var krogerTasks = ingredients.Select(async ing =>
            {
                await throttle.WaitAsync();     // ✅ wait until a slot is available
                try
                {
                    var results = await _krogerService.SearchProductByFilter(ing.Name);
                    return (ing.Name, results);
                }
                finally
                {
                    throttle.Release();         // ✅ return the slot no matter what
                }
            }).ToList();

            var krogerResults = await Task.WhenAll(krogerTasks);
            var krogerMap = krogerResults.ToDictionary(x => x.Name, x => x.results);


            foreach (var ingredient in ingredients)
            {
                var ingredientPreview = new ImportPreviewIngredient
                {
                    Name = ingredient.Name,
                    Unit = ingredient.Unit,
                    Amount = ingredient.Amount,
                };

                var ingredientName = ingredient.CleanName.ToLowerInvariant();
                var tokens = ingredientName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();

                var bestIngredient = allIngredients
                    .Where(kvp => tokens.Any(t => kvp.Key.Contains(t)))
                    .Select(kvp => new
                    {
                        IngredientId = kvp.Value,
                        CanonicalName = kvp.Key,
                        Score = LevenshteinRatio(kvp.Key, ingredientName)
                    })
                    .Where(x => x.Score >= 0.70)
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if (bestIngredient != null)
                {
                    ingredientPreview.MatchedIngredientId = bestIngredient.IngredientId;
                    ingredientPreview.MatchedCanonicalName = bestIngredient.CanonicalName;

                    var defaultMap = await _context.IngredientKrogerProducts
                        .AsNoTracking()
                        .Where(m => m.IngredientId == bestIngredient.IngredientId && m.IsDefault)
                        .Select(m => new { m.Upc, Name = m.KrogerProduct.Name })
                        .FirstOrDefaultAsync();

                    if (defaultMap != null)
                    {
                        ingredientPreview.SuggestedProductUpc = defaultMap.Upc;
                        ingredientPreview.SuggestedProductName = defaultMap.Name;
                    }
                }

                var krogerProducts = krogerMap.TryGetValue(ingredient.Name, out var products);
                var krogerFuzzySearch = products?
                    .Select(c => new
                    {
                        Name = c.name,
                        Upc = c.upc,
                        Score = LevenshteinRatio(c.name, ingredient.Name) 
                    })
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if (krogerFuzzySearch != null)
                {
                    ingredientPreview.Kroger = new SuggestedKrogerProductPreview
                    {
                        Name = krogerFuzzySearch.Name,
                        Upc = krogerFuzzySearch.Upc,
                        ImageUrl = $"https://www.kroger.com/product/images/medium/front/{krogerFuzzySearch.Upc}"
                    };
                }

                ingredientList.Add(ingredientPreview);
            }

            return ingredientList;
        }

        public record ProductLookup(string Name, string? Upc, string NameLower);
        public record IngredientLookup(int Id, string ConicalName, List<IngredientKrogerProduct> krogerMappings);

    }
}
