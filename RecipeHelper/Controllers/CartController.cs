using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Services;

namespace RecipeHelper.Controllers
{
    public class CartController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly KrogerService _krogerService;
        private readonly KrogerAuthService _krogerAuthService;
        private readonly ILogger<AuthController> _logger;
        private DatabaseContext _context;

        public CartController(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<AuthController> logger, DatabaseContext context, KrogerAuthService krogerAuthService, KrogerService krogerService)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
            _context = context;
            _krogerAuthService = krogerAuthService;
            _krogerService = krogerService;
        }

        // GET: CartController
        public ActionResult Index()
        {
            return View();
        }

        // GET: CartController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: CartController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: CartController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> AddToCart(AddToCartVM vm)
        {

            if (!vm.Items.Any())
            {
                TempData["ErrorMessage"] = "No valid ingredients were found to add to your Kroger cart.";
                return RedirectToAction("ReviewDinnerSelections", "Dinner");
            }

            try
            {
                var itemCount = vm.Items.Count;
                var returnUrl = Request.Path + Request.QueryString;
                var auth = await _krogerAuthService.EnsureAccessTokenAsync(returnUrl);

                if (!auth.IsAuthorized)
                {
                    return Redirect(auth.RedirectUrl);
                }



                var result = await _krogerService.AddToCart(new AddToCartRequest(vm.Items), auth.AccessToken);

                TempData["SuccessMessage"] = $"{itemCount} item{(itemCount == 1 ? "" : "s")} were added to your Kroger cart. " + "You can review or edit them in the Kroger app.";
                return RedirectToAction("Recipe", "Recipe"); // or "Index", "Recipe"
            }
            catch
            {
                TempData["ErrorMessage"] = "There was a problem adding items to your Kroger cart. Please try again.";
                // You can redirect back to the review page instead if you prefer
                return RedirectToAction("ReviewDinnerSelections", "Dinner");
                
            }
        }

        // GET: CartController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: CartController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: CartController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: CartController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }
    }
}
