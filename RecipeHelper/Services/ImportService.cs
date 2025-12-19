using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NuGet.Protocol;
using RecipeHelper.Models;
using RecipeHelper.Models.Import;
using RecipeHelper.Utility;
using RecipeHelper.ViewModels;

namespace RecipeHelper.Services
{
    public class ImportService
    {
        public DatabaseContext _context;
        public ILogger<ImportService> _logger;
        public KrogerService _krogerService;
        public ImportService(DatabaseContext context, ILogger<ImportService> logger, KrogerService krogerService)
        {
            _context = context;
            _logger = logger;
            _krogerService = krogerService;
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

            var allProducts = await _context.Products.AsNoTracking()
                .Select(p => new ProductLookup(p.Id, p.Name, p.Upc, p.Name.ToLower()))
                .ToListAsync();

            // merge any duplicates
            var mergedIngredients = ingredients
                        .GroupBy(i => new
                        {
                            Name = i.Name.Trim().ToLowerInvariant(),
                            Unit = MeasurementHelper.NormalizeMeasurementUnit(i.Unit) ?? i.Unit
                        })
                        .Select(g => new PreviewImportedRecipeIngredient
                        {
                            Name = g.Key.Name,
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

                var exactSearch = allProducts.FirstOrDefault(p =>
                    p.NameLower == ingredient.Name ||
                    p.NameLower.Contains(ingredient.Name)
                );

                if (exactSearch != null)
                {
                    ingredientPreview.SuggestedProductId = exactSearch.Id;
                    ingredientPreview.SuggestedProductName = exactSearch.Name;
                    ingredientPreview.SuggestedProductUpc = exactSearch.Upc;
                    ingredientPreview.SuggestionKind = "Exact";
                    ingredientList.Add(ingredientPreview);
                }
                else
                {
                    ingredientsNoExactMatch.Add(ingredient);
                }
            }

            var fuzzySearchIngredients = await IngredientFuzzySearch(ingredientsNoExactMatch, allProducts);
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
                RecipeProducts = new List<RecipeProduct>()
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

                if (ingredient.UseKroger)
                {
                    _logger.LogInformation($"Looking up Kroger product [{ingredient.Name}] [{ingredient.Upc}] to add to database");

                    var productDetails = await _krogerService.GetProductDetails(ingredient.Upc);
                    productDetails?.RemoveKrogerBrandFromName();

                    Product newProduct = new Product
                    {
                        Name = productDetails?.name ?? ingredient.Name,
                        Upc = productDetails?.upc ?? ingredient.Upc,
                        Price = (decimal)(productDetails?.regularPrice ?? 0)
                    };

                    await _context.Products.AddAsync(newProduct);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Added [{newProduct.Name}] to recipe. Amount [{ingredient.Amount}] Measurement [{ingredient.Unit}]");
                    var normalizedMeasurementUnit = MeasurementHelper.NormalizeMeasurementUnit(ingredient.Unit);

                    newRecipe.RecipeProducts.Add(new RecipeProduct
                    {
                        ProductId = newProduct.Id,
                        Quantity = ingredient.Amount,
                        MeasurementId = measurementDict.TryGetValue(normalizedMeasurementUnit, out var id) ? id : (int?)null
                    });
                }
                else
                {
                    var normalizedMeasurementUnit = MeasurementHelper.NormalizeMeasurementUnit(ingredient.Unit);

                    newRecipe.RecipeProducts.Add(new RecipeProduct
                    {
                        ProductId = (int)ingredient.ProductId,
                        Quantity = ingredient.Amount,
                        MeasurementId = measurementDict.TryGetValue(normalizedMeasurementUnit, out var id) ? id : (int?)null
                    });

                    _logger.LogInformation($"Added [{ingredient.Name}] to recipe. Amount [{ingredient.Amount}] Measurement [{ingredient.Unit}]");

                }
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


        private async Task<List<ImportPreviewIngredient>> IngredientFuzzySearch(List<PreviewImportedRecipeIngredient> ingredients, List<ProductLookup> products)
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
            var krogerMap = krogerResults.ToDictionary(x => x.Name.ToLowerInvariant(), x => x.results);


            foreach (var ingredient in ingredients)
            {
                var ingredientPreview = new ImportPreviewIngredient
                {
                    Name = ingredient.Name,
                    Unit = ingredient.Unit,
                    Amount = ingredient.Amount,
                };

                var ingredientName = ingredient.Name.ToLowerInvariant();
                var tokens = ingredientName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();

                var dbFuzzyMatch = products
                    .Where(p => tokens.Any(t => p.NameLower.Contains(t)))
                    .Take(50)
                    .Select(c => new
                    {
                        c.Id,
                        c.Name,
                        c.Upc,
                        Score = LevenshteinRatio(c.Name, ingredient.Name) // 0..1 if you wrote it that way
                    })
                    .Where(c => c.Score >= .5)
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                if (dbFuzzyMatch != null)
                {
                    ingredientPreview.SuggestedProductId = dbFuzzyMatch.Id;
                    ingredientPreview.SuggestedProductName = dbFuzzyMatch.Name;
                    ingredientPreview.SuggestedProductUpc = dbFuzzyMatch.Upc;
                    ingredientPreview.SuggestionKind = "Fuzzy";
                }

                if (krogerMap.TryGetValue(ingredientName, out var krogerProducts) && krogerProducts != null)
                {
                    var krogerFuzzySearch = krogerProducts
                    .Select(c => new
                    {
                        c.ProductId,
                        c.name,
                        c.upc,
                        Score = LevenshteinRatio(c.name, ingredient.Name) // 0..1 if you wrote it that way
                    })
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                    if (krogerFuzzySearch != null)
                    {
                        ingredientPreview.Kroger = new SuggestedKrogerProductPreview
                        {
                            Name = krogerFuzzySearch.name,
                            Upc = krogerFuzzySearch.upc,
                            ImageUrl = $"https://www.kroger.com/product/images/large/front/{krogerFuzzySearch.upc}"
                        };
                    }
                }

                ingredientList.Add(ingredientPreview);
            }

            return ingredientList;
        }

        public record ProductLookup(int Id, string Name, string? Upc, string NameLower);

    }
}
