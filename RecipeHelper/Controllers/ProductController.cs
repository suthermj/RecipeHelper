using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Models.Products;
using RecipeHelper.Services;

namespace RecipeHelper.Controllers
{
    public class ProductRecipeUsage
    {
        public int RecipeId { get; set; }
        public string RecipeName { get; set; } = "";
        public string? ImageUrl { get; set; }
    }

    public class ProductController : Controller
    {
        private readonly DatabaseContext _context;
        private readonly KrogerService _krogerService;
        private readonly ProductService _productService;
        private readonly ILogger _logger;

        public ProductController(DatabaseContext context, KrogerService krogerService, ILogger<ProductController> logger, ProductService productService)
        {
            _context = context;
            _krogerService = krogerService;
            _logger = logger;
            _productService = productService;
        }

        //  Returns List<ViewProductVM> Model
        public async Task<ActionResult> Products()
        {
            var products = await _productService.GetProductsAsync();

            var vm = products.Select(p => new ViewProductVM
            {
                Name = p.Name,
                Upc = p.Upc
            }).ToList();

            return View(vm);
        }

        [HttpGet]
        public async Task<ActionResult> ViewProduct(string productId)
        {
            var product = await _productService.GetProductAsync(productId);

            if (product is null)
            {
                _logger.LogWarning("ViewProduct: no product found for ProductId={ProductId}.", productId);
                TempData["ErrorMessage"] = "Error finding some product details";
                return RedirectToAction("Products", "Product");
            }

            var krogerProduct = await _krogerService.GetProductDetails(product.Upc);
            if (krogerProduct is null || krogerProduct.HasMissingData())
            {
                _logger.LogWarning("ViewProduct: Kroger product details missing or incomplete. ProductId={ProductId}, Upc={Upc}, IsNull={IsNull}",
                    productId, product.Upc, krogerProduct is null);
                TempData["WarningMessage"] = "Some product details could not be retrieved.";
            }

            if (!string.IsNullOrWhiteSpace(product.Name))
                krogerProduct.name = product.Name;

            // One row per recipe, even if it links this product on several ingredient lines.
            ViewBag.UsedInRecipes = await _context.RecipeIngredients
                .AsNoTracking()
                .Where(ri => ri.SelectedKrogerUpc == product.Upc)
                .Select(ri => new ProductRecipeUsage
                {
                    RecipeId = ri.RecipeId,
                    RecipeName = ri.Recipe.Name,
                    ImageUrl = ri.Recipe.ThumbnailUri ?? ri.Recipe.ImageUri
                })
                .Distinct()
                .OrderBy(u => u.RecipeName)
                .ToListAsync();

            ViewBag.ProductId = product.Upc;
            return View(krogerProduct);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProduct(string productId, string updatedProductName)
        {
            if (string.IsNullOrWhiteSpace(updatedProductName))
                return BadRequest(new { error = "Name cannot be empty." });

            var product = await _context.KrogerProducts.FindAsync(productId);
            if (product is null)
                return NotFound(new { error = "Product not found." });

            product.Name = updatedProductName.Trim();
            await _context.SaveChangesAsync();
            return Ok(new { success = true, productId, name = product.Name });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(string productId)
        {
            var product = await _context.KrogerProducts
                .Include(p => p.IngredientMappings)
                .FirstOrDefaultAsync(p => p.Upc == productId);
            if (product is null)
                return RedirectToAction("Products");

            // Recipes link products with a hard FK, so unlink those ingredient lines first;
            // the ingredients stay in their recipes, just without a "buy this" product.
            var recipeLines = await _context.RecipeIngredients.Where(ri => ri.SelectedKrogerUpc == productId).ToListAsync();
            foreach (var line in recipeLines) line.SelectedKrogerUpc = null;

            // Shopping-list items keep their text; they just lose the product link.
            var listItems = await _context.ShoppingListItems.Where(i => i.Upc == productId).ToListAsync();
            foreach (var item in listItems) item.Upc = null;

            // Pantry items match by UPC first, then name; keep the item, drop the dead UPC.
            var pantryItems = await _context.PantryItems.Where(p => p.KrogerUpc == productId).ToListAsync();
            foreach (var item in pantryItems) item.KrogerUpc = null;

            // If this was an ingredient's default product, promote another of its mappings.
            var defaultedIngredientIds = product.IngredientMappings.Where(m => m.IsDefault).Select(m => m.IngredientId).ToList();
            if (defaultedIngredientIds.Count > 0)
            {
                var replacements = await _context.IngredientKrogerProducts
                    .Where(m => defaultedIngredientIds.Contains(m.IngredientId) && m.Upc != productId)
                    .OrderBy(m => m.CreatedUtc)
                    .ToListAsync();
                foreach (var m in replacements.GroupBy(m => m.IngredientId).Select(g => g.First()))
                    m.IsDefault = true;
            }

            _context.IngredientKrogerProducts.RemoveRange(product.IngredientMappings);
            _context.KrogerProducts.Remove(product);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeleteProduct failed for {Upc}", productId);
                TempData["ErrorMessage"] = "Failed to delete product.";
                return RedirectToAction("ViewProduct", new { productId });
            }

            TempData["SuccessMessage"] = "Product successfully deleted";
            return RedirectToAction("Products");
        }

        public ActionResult AddProduct()
        {
            return View(new ProductSearchVM());
        }

        public async Task<ActionResult> SearchProduct(string searchTerm)
        {

            // Kroger caps a page at 50. Fetch that many up front; the view pages through
            // them 10 at a time and filters by brand client-side, so selections survive.
            var products = await _krogerService.SearchProductByFilter(searchTerm, limit: 50);

            if (products != null)
            {
                return View("AddProduct", new ProductSearchVM
                {
                    SearchTerm = searchTerm,
                    ProductSearchResults = products
                });
            }
            return View("AddProduct", new ProductSearchVM
            {
                SearchTerm = searchTerm
            });
        }


        [HttpGet]
        public async Task<IActionResult> SearchKroger(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return Json(Array.Empty<object>());

            var products = await _krogerService.SearchProductByFilter(term);

            if (products == null || !products.Any())
                return Json(Array.Empty<object>());

            // Shape the response for your dropdown
            var result = products.Select(p => new
            {
                name = p.name,                // or whatever property you're using
                upc = p.upc,
                size = p.size,
                brand = p.brand,
                imageUrl = $"https://www.kroger.com/product/images/medium/front/{p.upc}",               // or build from UPC
                id = p.ProductId                            // if you ever store it locally
            });

            return Json(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProducts(List<KrogerDatabaseProduct> productsToAdd)
        {
            _logger.LogInformation("CreateProducts started. RequestedCount={RequestedCount}", productsToAdd?.Count ?? 0);
            var result = await _productService.AddProducts(productsToAdd);
            _logger.LogInformation("CreateProducts completed. RequestedCount={RequestedCount}", productsToAdd?.Count ?? 0);

            return Ok(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSelectedProducts(List<string> selectedProducts, Dictionary<string, string> productNames)
        {
            selectedProducts ??= new List<string>();
            var productsToAdd = selectedProducts
                .Where(upc => !string.IsNullOrWhiteSpace(upc))
                .Select(upc => new KrogerDatabaseProduct
                {
                    Upc = upc,
                    Name = (productNames != null && productNames.TryGetValue(upc, out var name) && !string.IsNullOrWhiteSpace(name))
                        ? name
                        : upc
                })
                .ToList();

            _logger.LogInformation("AddSelectedProducts started. RequestedCount={RequestedCount}", productsToAdd.Count);
            var success = await _productService.AddProducts(productsToAdd);
            _logger.LogInformation("AddSelectedProducts completed. RequestedCount={RequestedCount}, Success={Success}", productsToAdd.Count, success);

            TempData["SuccessMessage"] = success
                ? (productsToAdd.Count == 1 ? "Added 1 product." : $"Added {productsToAdd.Count} products.")
                : null;
            TempData["ErrorMessage"] = success
                ? null
                : "Could not add the selected products.";

            return RedirectToAction("Products", "Product");
        }

        [HttpGet]
        public async Task<IActionResult> SearchDb(string term)
        {
            term ??= "";

            var products = await _productService.SearchForDbProduct(term);

            return Json(products);
        }

        public class ReplaceProductRequest
        {
            public string? OldUpc { get; set; }
            public string? NewUpc { get; set; }
            public List<int>? RecipeIds { get; set; }
        }

        /// <summary>
        /// Re-points every ingredient line in the chosen recipes that links OldUpc at NewUpc.
        /// Only RecipeIngredient.SelectedKrogerUpc changes; the Ingredient ↔ product default
        /// mappings are left alone.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplaceProductInRecipes([FromBody] ReplaceProductRequest req)
        {
            var oldUpc = req?.OldUpc?.Trim();
            var newUpc = req?.NewUpc?.Trim();
            var recipeIds = req?.RecipeIds?.Distinct().ToList() ?? new List<int>();

            if (string.IsNullOrEmpty(oldUpc) || string.IsNullOrEmpty(newUpc))
                return BadRequest(new { error = "Both the current and the new product are required." });
            if (oldUpc == newUpc)
                return BadRequest(new { error = "Pick a different product." });
            if (recipeIds.Count == 0)
                return BadRequest(new { error = "Select at least one recipe." });

            // SelectedKrogerUpc is a real FK to KrogerProduct.Upc, so the new product must exist locally.
            if (!await _context.KrogerProducts.AnyAsync(p => p.Upc == newUpc))
            {
                var details = await _krogerService.GetProductDetails(newUpc);
                if (details is null || string.IsNullOrWhiteSpace(details.upc))
                    return BadRequest(new { error = "Couldn't load that product from Kroger." });

                _context.KrogerProducts.Add(new KrogerProduct { Upc = details.upc, Name = details.name ?? details.upc });
                await _context.SaveChangesAsync();
            }

            var lines = await _context.RecipeIngredients
                .Where(ri => ri.SelectedKrogerUpc == oldUpc && recipeIds.Contains(ri.RecipeId))
                .ToListAsync();

            foreach (var line in lines)
                line.SelectedKrogerUpc = newUpc;

            await _context.SaveChangesAsync();

            _logger.LogInformation("ReplaceProductInRecipes: {OldUpc} -> {NewUpc}, Recipes={Recipes}, Lines={Lines}",
                oldUpc, newUpc, lines.Select(l => l.RecipeId).Distinct().Count(), lines.Count);

            return Json(new { recipes = lines.Select(l => l.RecipeId).Distinct().Count(), lines = lines.Count });
        }
    }
}
