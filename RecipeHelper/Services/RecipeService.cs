using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Routing.Matching;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NuGet.Protocol.Plugins;
using RecipeHelper.Models;
using RecipeHelper.Models.Kroger;
using RecipeHelper.ViewModels;
using static System.Net.WebRequestMethods;

namespace RecipeHelper.Services
{
    public class RecipeService
    {
        private readonly ILogger<RecipeService> _logger;
        private DatabaseContext _context;
        private KrogerService _krogerService;
        private ProductService _productService;
        public RecipeService(ILogger<RecipeService> logger, DatabaseContext context, KrogerService krogerService, ProductService productService)
        {
            _context = context;
            _logger = logger;
            _krogerService = krogerService;
            _productService = productService;
        }

        public Task<bool> VerifyRecipeName { get; set; }


        public async Task<MappedImportPreviewVM> AddImportedRecipePreview(PreviewImportedRecipeVM importedRecipe)
        {
            var title = importedRecipe.Title;
            var imageUri = importedRecipe.Image;
            var ingredients = importedRecipe.Ingredients;
            if (title is null)
            {
                _logger.LogError("Recipe title is null");
                return null;
            }

            MappedImportPreviewVM importPreview = new MappedImportPreviewVM
            {
                Title = title,
                Image = imageUri
            };

            var currentProducts = _context.Products.Select(p => new ViewProductVM
            {
                Name = p.Name,
                Upc = p.Upc,
                Id = p.Id,
            }).ToList();

            var ingredientList = new List<IngredientPreviewVM>();
            var availableMeasurements = _context.Measurements.Select(m => new
            {
                Id = m.Id.ToString(),
                Name = m.Name
            }).ToList();

            var allProducts = _context.Products.AsNoTracking().Select(matches => new
                   {
                       Name = matches.Name,
                       Upc = matches.Upc,
                       Id = matches.Id,
                       //Score = LevenshteinRatio(matches.Name.ToLower(), ingredient.Name.ToLower())
                   }).ToList();

            foreach (var ingredient in ingredients)
            {
                var ingredientPreview = new IngredientPreviewVM
                {
                    Name = ingredient.Name,
                    Unit = ingredient.Unit,
                    Amount = ingredient.Amount,

                };

                ingredient.Name = ingredient.Name.ToLower();

                var exactSearch = _context.Products.Where(p =>
                    p.Name.ToLower() == ingredient.Name.ToLower() ||
                    p.Name.ToLower().Contains(ingredient.Name.ToLower()) 
                ).Select(matches => new
                   {
                       Name = matches.Name,
                       Upc = matches.Upc,
                       Id = matches.Id,
                       //Score = LevenshteinRatio(matches.Name.ToLower(), ingredient.Name.ToLower())
                   }).FirstOrDefault();

                if (exactSearch != null)
                {
                    ingredientPreview.SuggestedProductId = exactSearch.Id;
                    ingredientPreview.SuggestedProductName = exactSearch.Name;
                    ingredientPreview.SuggestionKind = "Exact";
                }
                else
                {
                    var dbFuzzyMatch = allProducts
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
                        ingredientPreview.SuggestionKind = "Fuzzy";
                    }
                }

                var krogerProducts = _krogerService.SearchProductByFilter(ingredient.Name).Result;

                if (krogerProducts != null)
                {
                    var krogerFuzzySearch = krogerProducts
                    .Select(c => new
                    {
                        c.ProductId,
                        c.description,
                        c.upc,
                        c.regularPrice,
                        c.promoPrice,
                        c.onSale,
                        Score = LevenshteinRatio(c.description, ingredient.Name) // 0..1 if you wrote it that way
                    })
                    // .Where(c => c.Score > .3)
                    .OrderByDescending(x => x.Score)
                    .FirstOrDefault();

                    if (krogerFuzzySearch != null)
                    {
                        ingredientPreview.Kroger = new KrogerPreviewVM
                        {
                            Name = krogerFuzzySearch.description,
                            OnSale = krogerFuzzySearch.onSale,
                            Upc = krogerFuzzySearch.upc,
                            PromoPrice = (decimal?)krogerFuzzySearch.promoPrice,
                            RegularPrice = (decimal?)krogerFuzzySearch.regularPrice,
                            ImageUrl = $"https://www.kroger.com/product/images/xlarge/front/{krogerFuzzySearch.upc}"
                        };
                    }
                }

                ingredientList.Add(ingredientPreview);
            }

            importPreview.Ingredients = ingredientList;
            return importPreview;
        }

        public async Task<int?> SaveImportedRecipe(ConfirmMappingVM recipe)
        {
            await using var tx = await _context.Database.BeginTransactionAsync();

            var newRecipe = new Recipe
            {
                Name = recipe.Title,
                ImageUri = recipe.Image,
                RecipeProducts = new List<RecipeProduct>()
            };

            /*try
            {
                await _context.Recipes.AddAsync(newRecipe);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating recipe");
                return;
            }*/

            var measurements = _context.Measurements.Select(m => new
            {
                Id = m.Id,
                Name = m.Name,
            }).ToList();

            // Build a case-insensitive lookup for measurements once
            var measurementDict = await _context.Measurements
                .AsNoTracking()
                .ToDictionaryAsync(m => m.Name, m => m.Id, StringComparer.OrdinalIgnoreCase);

            int? MapMeasurementId(string? unit)
            {
                if (string.IsNullOrWhiteSpace(unit)) return null;
                // normalize common abbreviations here if you want:
                // e.g. if (unit.Equals("tsp", StringComparison.OrdinalIgnoreCase)) unit = "Teaspoon";
                return measurementDict.TryGetValue(unit.Trim(), out var id) ? id : (int?)null;
            }

            if (!recipe.Ingredients.IsNullOrEmpty())
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    if (ingredient.Include == false) continue;

                    if (ingredient.UseKroger)
                    {
                        var productDetails = await _krogerService.GetProductDetails(ingredient.KrogerUpc);
                        Models.Product newProduct = new Models.Product
                        {
                            Name = productDetails?.description ?? ingredient.Name,
                            Upc = productDetails?.upc ?? ingredient.KrogerUpc,
                            Price = (decimal)(productDetails?.regularPrice ?? 0)
                        };

                        await _context.Products.AddAsync(newProduct);

                        newRecipe.RecipeProducts.Add(new RecipeProduct
                        {
                            Product = newProduct,                 // <- key point: set navigation, not ProductId
                            Quantity = (int?)ingredient.Amount ?? 0,
                            MeasurementId = MapMeasurementId(ingredient.Unit)
                        });
                    }
                    else
                    {
                        newRecipe.RecipeProducts.Add(new RecipeProduct
                        {
                            ProductId = (int)ingredient.ProductId,                 // <- key point: set navigation, not ProductId
                            Quantity = (int?)ingredient.Amount ?? 0,
                            MeasurementId = MapMeasurementId(ingredient.Unit)
                        });
                    }
                }
            }

            _context.Recipes.Add(newRecipe);

            try
            {
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                return newRecipe.Id;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Error saving recipe");
                return null;
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
    }
}

