using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NuGet.Protocol;
using RecipeHelper;
using RecipeHelper.Models;
using RecipeHelper.Models.Products;
using RecipeHelper.Services;

namespace RecipeHelper.Controllers
{
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

        public ActionResult Products()
        {

            var products = _context.Products.Select(p => new ViewProductVM
            {
                Name = p.Name,
                Upc = p.Upc,
                Id = p.Id,
            }).ToList();

            return View(products);
        }

        [HttpGet]
        public async Task<ActionResult> ViewProduct(int productId)
        {

            var product = await _context.Products
                .AsNoTracking()
                .Where(p => p.Id == productId)
                .Select(p => new ViewProductVM { Id = p.Id, Upc = p.Upc, Name = p.Name })
                .FirstOrDefaultAsync();

            if (product is null)
            {
                TempData["ErrorMessage"] = "Error finding some product details";
                return RedirectToAction("Products", "Product");
            }

            var kroger = await _krogerService.GetProductDetails(product.Upc);
            if (kroger.HasMissingData())
                TempData["WarningMessage"] = "Some product details could not be retrieved.";

            if (!string.IsNullOrWhiteSpace(product.Name))
                kroger.description = product.Name;

            ViewBag.ProductId = product.Id;
            return View(kroger);
        }

        public async Task<ActionResult> UpdateProduct(int productId, string updatedProductName)
        {
            if (string.IsNullOrWhiteSpace(updatedProductName))
            {
                //TempData["ErrorMessage"] = "Name cannot be empty.";
                return BadRequest(new { error = "Name cannot be empty." });
            }

            var product = await _context.Products.FindAsync(productId);
            if (product is null)
            {
                //TempData["ErrorMessage"] = "Error finding product.";
                return NotFound(new { error = "Product not found." });
            }

            product.Name = updatedProductName.Trim();

            try
            {
                await _context.SaveChangesAsync();
                //TempData["WarningMessage"] = "Product name updated.";
                return Ok(new { success = true, productId, name = product.Name });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product");
                //TempData["ErrorMessage"] = "Error updating product.";
                return StatusCode(500, new { error = "Error updating product." });
            }

        }

        public ActionResult AddProduct()
        {
            return View(new ProductSearchVM());
        }

        public async Task<ActionResult> SearchProduct(string searchTerm)
        {

            var products = await _krogerService.SearchProductByFilter(searchTerm);

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

        [HttpPost]
        public async Task<IActionResult> AddSelectedProducts(string[] selectedProducts, Dictionary<string, string> productNames, Dictionary<string, decimal> productPrices)
        {
            _logger.LogInformation("Adding [{totalToAdd}] products to database", selectedProducts?.Length);
            foreach (var upc in selectedProducts)
            {
                string name = productNames[upc];

                if (name.StartsWith("Kroger®"))
                {
                    name = name.Replace("Kroger®", "").Trim();
                }
                decimal price = productPrices[upc];

                // Logic to add each selected product to the database using UPC, name, and price
                try
                {
                    Product product = new Product
                    {
                        Name = name,
                        Price = price,
                        Upc = upc
                    };

                    _context.Products.Add(product);
                    _context.SaveChanges();
                    TempData["SuccessMessage"] = "Product(s) successfully added!";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "Failed to add products.";
                    _logger.LogError(ex, "Error saving product");
                }
            }

            // Redirect to a confirmation page or back to the product list
            return RedirectToAction("AddProduct", new ProductSearchVM());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int productId)
        {
            var product = await _context.Products.FindAsync(productId);

            if (product != null)
            {
                _logger.LogInformation("[DeleteProduct] Found product with id [{id}]", productId);
                try
                {
                    _context.Products.Remove(product);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message, "Error deleting product with id [{id}]", productId);
                    TempData["ErrorMessage"] = $"Failed to delete product.";
                    return RedirectToAction("ViewProduct");
                }
                _logger.LogInformation("[DeleteProduct] Deleted product [{productName}] with id [{id}]", product.Name, productId);
                TempData["SuccessMessage"] = "Product successfully deleted";

                return RedirectToAction("Products");
            }
            else
            {
                _logger.LogInformation("[DeleteProduct] recipe with id [{id}] not found", productId);
                return RedirectToAction("Products");
            }

        }

        [HttpGet]
        public async Task<IActionResult> SearchDb(string term)
        {
            term ??= "";

            var products = await _productService.SearchForDbProduct(term);

            return Json(products);
        }
    }
}
