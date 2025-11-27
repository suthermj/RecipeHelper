using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RecipeHelper.Models;
using RecipeHelper.Models.Dinner;
using RecipeHelper.Services;

namespace RecipeHelper.Controllers
{
    public class DinnerController : Controller
    {

        private DatabaseContext _context;

        private readonly ILogger<RecipeController> _logger;

        public DinnerController(ILogger<RecipeController> logger, DatabaseContext context)
        {
            _logger = logger;
            _context = context;
        }


        // GET: Dinner
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult SelectWeeklyRecipes()
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
                }).ToList(),
            }).ToList();

            return View(recipes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitDinnerSelections(List<int> selectedRecipes)
        {
            ReviewDinnerSelectionsVM model = new ReviewDinnerSelectionsVM
            {
                SelectedRecipes = new List<SelectedRecipeVM>(),
                Ingredients = new List<IngredientVM>()
            };

            var recipes = _context.Recipes.Where(r => selectedRecipes.Contains(r.Id)).Select(r => new ViewRecipeVM
            {
                RecipeName = r.Name,
                ImageUri = r.ImageUri,
                Ingredients = r.RecipeProducts.Select(rp => new IngredientVM
                {
                    Id = rp.Product.Id,
                    Name = rp.Product.Name,
                    Quantity = rp.Quantity,
                    Measurement = rp.Measurement.Name == null ? "Count" : rp.Measurement.Name
                }).ToList(),
            }).ToList();

            Dictionary<int, List<IngredientVM>> ingDict = new Dictionary<int, List<IngredientVM>>();
            List<IngredientVM> tempIngredients = new List<IngredientVM>();

            if (recipes != null)
            {
                foreach (var recipe in recipes)
                {
                    model.SelectedRecipes.Add(new SelectedRecipeVM
                    {
                        RecipeName = recipe.RecipeName,
                        ImageUri = recipe.ImageUri
                    });

                    foreach (var ingredient in recipe.Ingredients)
                    {
                        tempIngredients.Add(ingredient);
                        if (ingDict.ContainsKey(ingredient.Id))
                        {
                            ingDict[ingredient.Id].Add(new IngredientVM
                            {
                                Id = ingredient.Id,
                                Name = ingredient.Name,
                                Quantity = ingredient.Quantity,
                                Measurement = ingredient.Measurement
                            });
                        }
                        else
                        {
                            List<IngredientVM> ingredientList =
                            [
                                new IngredientVM
                                {
                                    Id = ingredient.Id,
                                    Name = ingredient.Name,
                                    Quantity = ingredient.Quantity,
                                    Measurement = ingredient.Measurement
                                },
                            ];
                            ingDict.Add(ingredient.Id, ingredientList);
                        }
                    }
                }
            }

            foreach (var ingredient in ingDict)
            {
                _logger.LogInformation("Processing ingredient ID: {ingredientId} with {count} entries", ingredient.Key, ingredient.Value.Count);
                var measurements = _context.Measurements.ToList();
                bool allSame = ingredient.Value.All(x => x.Measurement.Equals(ingredient.Value[0].Measurement));

                if (allSame)
                {
                    decimal totalQuantity = ingredient.Value.Sum(x => x.Quantity);
                    model.Ingredients.Add(new IngredientVM
                    {
                        Id = ingredient.Key,
                        Name = ingredient.Value[0].Name,
                        Quantity = totalQuantity,
                        Measurement = ingredient.Value[0].Measurement
                    });
                }
                else
                {
                    var uniqueValues = ingredient.Value
                        .Select(x => x.Measurement)
                        .Distinct()
                        .ToList();

                    
                    foreach (var measurement in uniqueValues)
                    {
                        var newAmount = 0m;
                        var quantities = ingredient.Value
                            .Where(x => x.Measurement.Equals(measurement))
                            .Select(x => x.Quantity)
                            .ToList()
                            .Sum(); 

                        var measurementType = measurements
                            .Where(m => m.Name.Equals(measurement))
                            .Select(m => m.MeasureType)
                            .FirstOrDefault();

                        if (measurementType == null)
                        {
                            newAmount += quantities;
                        }
                        else if (measurementType.Equals("Volume"))
                        {
                            if (measurement.Equals("Teaspoons"))
                            {
                                newAmount += quantities;
                            }
                            else if (measurement.Equals("Tablespoons"))
                            {
                                newAmount += quantities * 3m;
                            }
                            else if (measurement.Equals("Cups"))
                            {
                                newAmount += quantities * 48m;
                            }
                        }
                        
                        else if (measurementType.Equals("Weight"))
                        {
                             if (measurement.Equals("Ounces"))
                            {
                                newAmount += quantities;
                            }
                            else if (measurement.Equals("Pounds"))
                            {
                                newAmount += quantities *  16m;
                            }
                        }
                        
                        model.Ingredients.Add(new IngredientVM
                        {
                            Id = ingredient.Key,
                            Name = ingredient.Value[0].Name,
                            Quantity = newAmount,
                            Measurement = measurement
                        });
                    }
                }
            }

            return View("ReviewDinnerSelections", model);
        }

    }
}
