using System.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models;
using System.Diagnostics;
using RecipeHelper.Services;
using Newtonsoft.Json;
using System.Collections;


namespace RecipeHelper.Controllers
{
    public class RecipeController : Controller
    {
        private DatabaseContext _context;

        private readonly ILogger<RecipeController> _logger;
        private StorageService _storageService;

        public RecipeController(ILogger<RecipeController> logger, DatabaseContext context, StorageService storageService)
        {
            _logger = logger;
            _context = context;
            _storageService = storageService;
        }

        public ActionResult Recipe()
        {

            var recipes = _context.Recipes.Select(r => new ViewRecipeVM
            {
                Id = r.Id,
                RecipeName = r.Name,
                ImageUri = r.ImageUri,
                Ingredients = r.RecipeProducts.Select(rp => new IngredientNameVM
                {
                    Name = rp.Product.Name,
                    Quantity = rp.Quantity,
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
                Ingredients = r.RecipeProducts.Select(rp => new IngredientNameVM
                {
                    Name = rp.Product.Name,
                    Quantity = rp.Quantity,
                }).ToList(),
            }).FirstOrDefault();

            return View(recipe);
        }

        public ActionResult SelectWeeklyRecipes()
        {
            var recipes = _context.Recipes.Select(r => new ViewRecipeVM
            {
                Id = r.Id,
                RecipeName = r.Name,
                ImageUri = r.ImageUri,
                Ingredients = r.RecipeProducts.Select(rp => new IngredientNameVM
                {
                    Name = rp.Product.Name,
                    Quantity = rp.Quantity,
                }).ToList(),
            }).ToList();

            return View(recipes);
        }

        public ActionResult SubmitDinnerSelections(List<int> selectedRecipes)
        {
            SubmitDinnerSelectionsVM model = new SubmitDinnerSelectionsVM
            {
                SelectedRecipes = new List<SelectedRecipeVM>(),
                Ingredients = new Dictionary<string, int>()
            };

            var recipes = _context.Recipes.Where(r => selectedRecipes.Contains(r.Id)).Select(r => new ViewRecipeVM
            {
                RecipeName = r.Name,
                ImageUri = r.ImageUri,
                Ingredients = r.RecipeProducts.Select(rp => new IngredientNameVM
                {
                    Name = rp.Product.Name,
                    Quantity = rp.Quantity,
                }).ToList(),
            }).ToList();

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
                        if (model.Ingredients.ContainsKey(ingredient.Name))
                        {
                            model.Ingredients[ingredient.Name] += ingredient.Quantity;
                        }
                        else
                        {
                            model.Ingredients.Add(ingredient.Name, ingredient.Quantity);
                        }
                    }
                }
            }

            return View("ReviewDinnerSelections", model);
        }


        [HttpPost]
        public IActionResult SaveIngredients(IngredientsVM model)
        {
            _logger.LogInformation("howe");

            var ingredients = model.Ingredients;

            var chosenIngredients = ingredients.Where(i => i.Quantity > 0);

            foreach (var ingredient in chosenIngredients)
            {
                var recipeProduct = new RecipeProduct
                {
                    RecipeId = model.RecipeId,
                    ProductId = ingredient.Id,
                    Quantity = ingredient.Quantity,
                };
                _context.RecipeProducts.Add(recipeProduct);
                _context.SaveChanges();
            }

            var recipeToReview = _context.Recipes.Where(r => r.Id == model.RecipeId).Select(r => new ViewRecipeVM
            {
                Id = r.Id,
                RecipeName = r.Name,
                ImageUri = r.ImageUri,
                Ingredients = r.RecipeProducts.Select(rp => new IngredientNameVM
                {
                    Name = rp.Product.Name,
                    Quantity = rp.Quantity,
                }).ToList(),
            }).FirstOrDefault();
            return View("ReviewRecipe", recipeToReview);
        }

       /* 
        /*
        public ActionResult IngredientsPicker()
        {
            var products = _context.Products.Select(p => new ProductVM
            {
                Name = p.Name,
                Upc = p.Upc,
                Id = p.Id
            }).ToList();

            var ingredients = new IngredientsVM
            {
                Ingredients = products
            };

            ViewBag.Ingredients = ingredients;

            return View("Product", new IngredientsVM { Ingredients = new List<ProductVM>() });
        }
        */
        public ActionResult NewRecipe()
        {
            return View();
        }
        public ActionResult CreateEditRecipe()
        {
            /*_storageService.StoreRecipeImage(newRecipe.ImageFile);
            try
             {

                 var recipe = new Recipe
                 {
                     Name = recipeCreateRequest.RecipeName,
                     ImageUri = recipeCreateRequest.ImageUri,
                 };
                 _context.Recipes.Add(recipe);
                 _context.SaveChanges();

                 foreach (var ingredient in recipeCreateRequest.Ingredients)
                 {
                     var recipeProduct = new RecipeProduct
                     {
                         RecipeId = recipe.Id,
                         ProductId = ingredient.Id,
                         Quantity = ingredient.Quantity,
                     };
                     _context.RecipeProducts.Add(recipeProduct);
                     _context.SaveChanges();
                 }

                 var createdRecipe = _context.Recipes.Where(r => r.Id == recipe.Id).Select(r => new RecipeVM
                 {
                     RecipeName = r.Name,
                     ImageUri = r.ImageUri,
                     Ingredients = r.RecipeProducts.Select(rp => new IngredientNameVM
                     {
                         Name = rp.Product.Name,
                         Quantity = rp.Quantity,
                     }).ToList(),
                 });
                 return Ok(createdRecipe);
             }
             catch (Exception ex)
             {
                 return BadRequest(ex.Message);
             }

        //return View();

        var products = _context.Products.Select(p => new ProductVM
            {
                Name = p.Name,
                Upc = p.Upc,
                Id = p.Id
                //ImageUri = r.ImageUri,
                /*Ingredients = r.RecipeProducts.Select(rp => new IngredientNameVM
                {
                    Name = rp.Product.Name,
                    Quantity = rp.Quantity,
                }).ToList(),
            }).ToList();

            return View("Products", products);
        }
*/
            return View();
        }

        public async Task<ActionResult> CreateEditRecipeForm(CreateRecipeVM newRecipe)
        {
            var recipeExists = await _context.Recipes.Where(r => r.Name == newRecipe.RecipeName).FirstOrDefaultAsync();

            // Serialize the model to JSON
            var jsonString = JsonConvert.SerializeObject(newRecipe);

            // Set the JSON string in session
            HttpContext.Session.SetString("Recipe", jsonString);

            if (recipeExists != null)
            {
                _logger.LogInformation($"Recipe name [{newRecipe.RecipeName}] already exists");
                TempData["ErrorMessage"] = "Please correct the errors before proceeding.";
                return RedirectToAction("CreateEditRecipe");
            }

            StoreImageBlobResponse blobResponse = new();
            if (newRecipe.ImageFile != null)
            {
                _logger.LogInformation($"storing recipe image in blob storage [{newRecipe.ImageFile.FileName}]");
                blobResponse = await _storageService.StoreRecipeImage(newRecipe.ImageFile);
            }

            var recipe = new Recipe
            {
                Name = newRecipe.RecipeName,
                ImageUri = blobResponse.BlobUri,
            };

            try
            {
                _context.Recipes.Add(recipe);
                _context.SaveChanges();

                /*foreach (var ingredient in newRecipe.Ingredients)
                {
                    var recipeProduct = new RecipeProduct
                    {
                        RecipeId = recipe.Id,
                        ProductId = ingredient.Id,
                        Quantity = ingredient.Quantity,
                    };
                    _context.RecipeProducts.Add(recipeProduct);
                    _context.SaveChanges();
                }*/
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }

            var products = _context.Products.Select(p => new ProductVM
            {
                Name = p.Name,
                Upc = p.Upc,
                Id = p.Id
            }).ToList();

            var ingredients = new IngredientsVM
            {
                Ingredients = products
            };

            //ViewBag.Ingredients = ingredients;


            return View("ProductToChoose", new IngredientsVM { RecipeId = recipe.Id, Ingredients = products });

            //return RedirectToAction("IngredientsPicker");


        }

        //[HttpDelete("{id}")]
        public ActionResult DeleteRecipe(int id)
        {
            _logger.LogInformation("[DeleteRecipe] Finding recipe with id [{id}]", id);
            var recipe = _context.Recipes.Find(id);

            if (recipe != null)
            {
                _logger.LogInformation("[DeleteRecipe] Found recipe with id [{id}]", id);
                _context.Recipes.Remove(recipe);
                _context.SaveChanges();
                _logger.LogInformation("[DeleteRecipe] Deleted recipe [{recipeName}] with id [{id}]", recipe.Name, id);
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
