using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models;
using RecipeHelper.Models.Kroger;
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
                TempData["ErrorMessage"] = "Error finding some product details";
                return RedirectToAction("Products", "Product");
            }

            var krogerProduct = await _krogerService.GetProductDetails(product.Upc);
            if (krogerProduct is null || krogerProduct.HasMissingData())
                TempData["WarningMessage"] = "Some product details could not be retrieved.";

            if (!string.IsNullOrWhiteSpace(product.Name))
                krogerProduct.name = product.Name;

            ViewBag.ProductId = product.Upc;
            return View(krogerProduct);
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
                imageUrl = $"https://www.kroger.com/product/images/medium/front/{p.upc}",               // or build from UPC
                id = p.ProductId                            // if you ever store it locally
            });

            return Json(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProducts(List<KrogerDatabaseProduct> productsToAdd)
        {
            var result = await _productService.AddProducts(productsToAdd);

            return Ok(result);
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
