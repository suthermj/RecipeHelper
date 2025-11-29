using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Services;

namespace RecipeHelper.Controllers
{
    public class CartController : Controller
    {
        private readonly KrogerService _krogerService;
        private readonly KrogerAuthService _krogerAuthService;
        private readonly ILogger<AuthController> _logger;
        private DatabaseContext _context;

        public CartController(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<AuthController> logger, DatabaseContext context, KrogerAuthService krogerAuthService, KrogerService krogerService)
        {
            _logger = logger;
            _context = context;
            _krogerAuthService = krogerAuthService;
            _krogerService = krogerService;
        }

        [HttpGet]
        public async Task<IActionResult> ViewCart()
        {
            var returnUrl = Url.Action(nameof(ViewCart), "Cart");

            var auth = await _krogerAuthService.EnsureAccessTokenAsync(returnUrl);

            if (!auth.IsAuthorized)
            {
                // User is not authorized → redirect them to Kroger login.
                // After login, your Auth callback should LocalRedirect(returnUrl),
                // which will call THIS action again.
                return Redirect(auth.RedirectUrl);
            }

            var vm = await _krogerService.GetCurrentCartItems(auth.AccessToken!);

            return View(vm);
        }

        // Called when user clicks "Add all items to cart"
        [HttpPost]
        public async Task<IActionResult> BeginAddToCart(AddToCartVM vm)
        {
            _logger.LogInformation("AddToCart called with {ItemCount} items.", vm.Items.Count);

            if (!vm.Items.Any())
            {
                TempData["ErrorMessage"] = "No valid ingredients were found to add to your Kroger cart.";
                return RedirectToAction("ReviewDinnerSelections", "Dinner");
            }

            vm.Items = vm.Items.Where(i => i.Include).ToList();

            // 1. Store the model somewhere (Session / TempData / DB)
            HttpContext.Session.SetString("PendingCart",
                JsonSerializer.Serialize(vm));

            var auth = await _krogerAuthService.EnsureAccessTokenAsync();

            // 2. Check if user already has valid Kroger auth
            if (!auth.IsAuthorized)
            {
                var returnUrl = Url.Action("CompleteAddToCart", "Cart");
                return RedirectToAction("Login", "Auth", new { returnUrl });
            }

            // 3. Already authorized → go straight to completion
            return RedirectToAction("CompleteAddToCart");
        }

        // Called AFTER auth is done (GET – safe to redirect to)
        [HttpGet]
        public async Task<IActionResult> CompleteAddToCart()
        {
            var token = await _krogerAuthService.GetKrogerAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                // If this happens, either redirect them to authorization again
                // or back to the review page with an error message.
                TempData["ErrorMessage"] = "Your Kroger session expired. Please try adding items again.";
                return RedirectToAction("ReviewDinnerSelections", "Dinner");
            }

            var pendingCartJson = HttpContext.Session.GetString("PendingCart");
            if (string.IsNullOrEmpty(pendingCartJson))
            {
                // nothing to process, fallback somewhere sensible
                return RedirectToAction("SelectWeeklyRecipes", "Dinner");
            }

            var vm = JsonSerializer.Deserialize<AddToCartVM>(pendingCartJson);

            try
            {
                var itemCount = vm.Items.Count;
                var cartRequest = await _krogerService.ConvertIngredientsToCartItems(vm);
                var result = await _krogerService.AddToCart(cartRequest, token);

                // Optional: clear it after use
                HttpContext.Session.Remove("PendingCart");

                TempData["SuccessMessage"] = $"{itemCount} item{(itemCount == 1 ? "" : "s")} were added to your Kroger cart. " + "You can review or edit them in the Kroger app.";
                return RedirectToAction("Recipe", "Recipe");
            }
            catch
            {
                TempData["ErrorMessage"] = "There was a problem adding items to your Kroger cart. Please try again.";
                return RedirectToAction("ReviewDinnerSelections", "Dinner");

            }
        }
    }
}
