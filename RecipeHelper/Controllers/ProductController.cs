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

        public ProductController(DatabaseContext context, KrogerService krogerService)
        {
            _context = context;
            _krogerService = krogerService;
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
    }
}
