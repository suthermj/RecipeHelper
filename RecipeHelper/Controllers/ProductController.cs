using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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
        private readonly ILogger _logger;

        public ProductController(DatabaseContext context, KrogerService krogerService, ILogger<ProductController> logger)
        {
            _context = context;
            _krogerService = krogerService;
            _logger = logger;
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

    }
}
