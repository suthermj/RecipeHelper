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

            var vm = await _krogerService.GetKrogerCartItemsAsync(auth.AccessToken!);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewAddToCart(AddToCartVM vm)
        {
            // vm.Items currently holds ingredients (with measurement/quantity/etc)
            var detailedCartItems = await _krogerService.ConvertIngredientsToCartItems(vm);

            // Build preview items by fetching product details for each UPC
            var previewItems = new List<AddToCartPreviewItemVM>();

            foreach (var cartItem in detailedCartItems)
            {
                // Adjust property names to your Product type
                previewItems.Add(new AddToCartPreviewItemVM
                {
                    Upc = cartItem.Upc,
                    QuantityToAdd = cartItem.Quantity,
                    Name = cartItem.Name,
                    Brand = cartItem.Brand,
                    StockLevel = cartItem.StockLevel,
                    Size = cartItem.KrogerPackSize ?? "",
                    Aisle = cartItem.Aisle ?? "",
                    RegularPrice = cartItem.RegularPrice,
                    PromoPrice = cartItem.PromoPrice,
                    Include = true,
                    ConversionNote = cartItem.ConversionNote,
                    OriginalIngredient = cartItem.OriginalIngredient,
                });
            }

            var previewVm = new AddToCartPreviewVM
            {
                Items = previewItems
            };

            return View("PreviewAddToCart", previewVm); // Views/Cart/PreviewAddToCart.cshtml
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
                var cartItems = await _krogerService.ConvertIngredientsToCartItems(vm);
                AddToCartRequest addToCartRequest = new AddToCartRequest
                {
                    Items = cartItems.Select(d => new CartItem
                    {
                        Upc = d.Upc,
                        Quantity = d.Quantity,
                    }).ToList()
                };


                var result = await _krogerService.AddToCartAsync(addToCartRequest, token);

                // Optional: clear it after use
                HttpContext.Session.Remove("PendingCart");

                TempData["SuccessMessage"] = $"{itemCount} item{(itemCount == 1 ? "" : "s")} were added to your Kroger cart. " + "You can review or edit them in the Kroger app.";
                return RedirectToAction("Recipe", "Recipe");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
                TempData["ErrorMessage"] = "There was a problem adding items to your Kroger cart. Please try again.";
                return RedirectToAction("SelectWeeklyRecipes", "Dinner");

            }
        }
    }
}
