using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Services;
using RecipeHelper.Utility;

namespace RecipeHelper.Controllers
{
    public class CartController : Controller
    {
        private const string PendingPreviewSessionKey = "PendingAddToCartPreview";

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
                _logger.LogInformation("ViewCart: not authorized, redirecting to Kroger login.");
                return Redirect(auth.RedirectUrl);
            }

            var vm = await _krogerService.GetKrogerCartItemsAsync(auth.AccessToken!);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PreviewAddToCart(AddToCartVM vm)
        {
            // Only ingredients the user left checked on the review screen should
            // reach the Kroger cart preview.
            vm.Items = vm.Items.Where(i => i.Include).ToList();
            _logger.LogInformation("PreviewAddToCart started. IncludedItemCount={IncludedItemCount}", vm.Items.Count);

            // vm.Items currently holds ingredients (with measurement/quantity/etc)
            var conversionResult = await _krogerService.ConvertIngredientsToCartItems(vm);
            var detailedCartItems = conversionResult.Items;

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
                Items = previewItems,
                Skipped = conversionResult.Skipped
            };

            _logger.LogInformation("PreviewAddToCart completed. PreviewItemCount={PreviewItemCount}, SkippedItemCount={SkippedItemCount}", previewItems.Count, conversionResult.Skipped.Count);

            // Redirect-after-post so this page has a GET URL that's safe to reload
            // (see the matching comment in DinnerController.SubmitDinnerSelections).
            PendingResultCache.Set(HttpContext.Session, PendingPreviewSessionKey, previewVm);
            return RedirectToAction(nameof(PreviewAddToCart));
        }

        // GET: Cart/PreviewAddToCart -- renders the preview computed above.
        [HttpGet]
        public IActionResult PreviewAddToCart()
        {
            if (!PendingResultCache.TryGet<AddToCartPreviewVM>(HttpContext.Session, PendingPreviewSessionKey, out var previewVm))
            {
                return RedirectToAction("SelectWeeklyRecipes", "Dinner");
            }

            return View(previewVm); // Views/Cart/PreviewAddToCart.cshtml
        }

        // Called when user clicks "Add all items to cart"
        [HttpPost]
        public async Task<IActionResult> BeginAddToCart(AddToCartVM vm)
        {
            _logger.LogInformation("AddToCart called with {ItemCount} items.", vm.Items.Count);

            if (!vm.Items.Any())
            {
                _logger.LogWarning("BeginAddToCart: no items to add, redirecting back to review.");
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
                _logger.LogWarning("CompleteAddToCart: no valid Kroger access token, redirecting back to review.");
                TempData["ErrorMessage"] = "Your Kroger session expired. Please try adding items again.";
                return RedirectToAction("ReviewDinnerSelections", "Dinner");
            }

            var pendingCartJson = HttpContext.Session.GetString("PendingCart");
            if (string.IsNullOrEmpty(pendingCartJson))
            {
                // nothing to process, fallback somewhere sensible
                _logger.LogWarning("CompleteAddToCart: no PendingCart in session, redirecting to SelectWeeklyRecipes.");
                return RedirectToAction("SelectWeeklyRecipes", "Dinner");
            }

            var vm = JsonSerializer.Deserialize<AddToCartVM>(pendingCartJson);

            try
            {
                var itemCount = vm.Items.Count;

                // vm.Items already carries the final per-item quantities the user
                // confirmed on the preview screen, so build the cart request directly
                // from them instead of re-running unit conversion (which would treat
                // the already-converted pack quantity as a raw ingredient amount).
                var addToCartRequest = new AddToCartRequest(vm.Items);

                var result = await _krogerService.AddToCartAsync(addToCartRequest, token);

                if (!result)
                {
                    // AddToCartAsync returns false (rather than throwing) on a non-2xx
                    // response from Kroger, so this doesn't hit the catch block below --
                    // it has to be checked explicitly or a rejected request reports as a
                    // successful cart add.
                    _logger.LogWarning("CompleteAddToCart: AddToCartAsync reported failure. ItemCount={ItemCount}", itemCount);
                    TempData["ErrorMessage"] = "There was a problem adding items to your Kroger cart. Please try again.";
                    return RedirectToAction("SelectWeeklyRecipes", "Dinner");
                }

                // Optional: clear it after use
                HttpContext.Session.Remove("PendingCart");

                _logger.LogInformation("CompleteAddToCart succeeded. ItemCount={ItemCount}", itemCount);
                TempData["SuccessMessage"] = $"{itemCount} item{(itemCount == 1 ? "" : "s")} were added to your Kroger cart. " + "You can review or edit them in the Kroger app.";
                return RedirectToAction("Recipe", "Recipe");
            }
            catch (Exception ex)
            {
                // Exception type/message are put directly in the message template (not
                // just passed as the LogError exception argument) -- see the matching
                // comment in StorageService.StoreRecipeImage for why.
                _logger.LogError(ex, "CompleteAddToCart failed. ItemCount={ItemCount}, ExceptionType={ExceptionType}, ExceptionMessage={ExceptionMessage}",
                    vm.Items.Count, ex.GetType().FullName, ex.Message);
                TempData["ErrorMessage"] = "There was a problem adding items to your Kroger cart. Please try again.";
                return RedirectToAction("SelectWeeklyRecipes", "Dinner");

            }
        }
    }
}
