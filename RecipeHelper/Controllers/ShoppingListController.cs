using Microsoft.AspNetCore.Mvc;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Models.Lists;
using RecipeHelper.Services;

namespace RecipeHelper.Controllers
{
    public class ShoppingListController : Controller
    {
        private readonly ILogger<ShoppingListController> _logger;
        private readonly ShoppingListService _shoppingListService;
        private readonly KrogerService _krogerService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public ShoppingListController(ILogger<ShoppingListController> logger, ShoppingListService shoppingListService, KrogerService krogerService, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _logger = logger;
            _shoppingListService = shoppingListService;
            _krogerService = krogerService;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            var lists = await _shoppingListService.GetAllAsync();
            return View(lists);
        }

        public async Task<IActionResult> ViewList(int id)
        {
            var list = await _shoppingListService.GetByIdAsync(id);
            if (list == null) return NotFound();
            ViewData["CurrentStoreId"]   = Request.Cookies["KrogerLocationId"] ?? _configuration["Kroger:mariemontLocationId"] ?? "01400421";
            ViewData["CurrentStoreName"] = Request.Cookies["KrogerLocationName"] ?? "Mariemont (Default)";
            return View(list);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name)
        {
            var list = await _shoppingListService.CreateAsync(name);
            return RedirectToAction(nameof(ViewList), new { id = list.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rename(int id, string name)
        {
            await _shoppingListService.RenameAsync(id, name);
            return RedirectToAction(nameof(ViewList), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddItem(int listId, string name, int quantity = 1)
        {
            await _shoppingListService.AddItemAsync(listId, name, quantity);
            return RedirectToAction(nameof(ViewList), new { id = listId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateItem(int itemId, int listId, int? quantity = null, bool? isCompleted = null)
        {
            await _shoppingListService.UpdateItemAsync(itemId, quantity, isCompleted);
            return RedirectToAction(nameof(ViewList), new { id = listId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveItem(int itemId, int listId)
        {
            await _shoppingListService.RemoveItemAsync(itemId);
            return RedirectToAction(nameof(ViewList), new { id = listId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromMealPlan(CreateFromMealPlanVM model)
        {
            var includedItems = model.Items.Where(i => i.Include).ToList();

            var storeId = _httpContextAccessor.HttpContext?.Request.Cookies["KrogerLocationId"]
                ?? _configuration["Kroger:mariemontLocationId"]
                ?? "01400421";

            var withUpc = includedItems.Where(i => !string.IsNullOrWhiteSpace(i.Upc)).ToList();
            var withoutUpc = includedItems.Where(i => string.IsNullOrWhiteSpace(i.Upc)).ToList();

            var addToCartVm = new AddToCartVM
            {
                Items = withUpc.Select(i => new CartItemVM
                {
                    Upc = i.Upc!,
                    Quantity = i.Quantity,
                    Measurement = i.Measurement ?? "",
                    Include = true
                }).ToList()
            };

            var cartItems = await _krogerService.ConvertIngredientsToCartItems(addToCartVm);

            var items = cartItems.Select(c => new ShoppingListItem
            {
                Name = c.Name,
                Quantity = c.Quantity,
                Upc = c.Upc,
                AisleNumber = c.Aisle != "N/A" ? c.Aisle : null,
                AisleDescription = InferSectionFromCategories(c.Categories),
                Brand = c.Brand,
                Price = c.RegularPrice > 0 ? (decimal?)c.RegularPrice : null,
                PromoPrice = c.PromoPrice > 0 ? (decimal?)c.PromoPrice : null,
            }).ToList();

            items.AddRange(withoutUpc.Select(i => new ShoppingListItem
            {
                Name = i.Name,
                Quantity = (int)Math.Ceiling(i.Quantity),
            }));

            var name = $"Meal Plan – {DateTime.Now:MMM d, yyyy}";
            var list = await _shoppingListService.CreateAsync(name, items, storeId);

            return RedirectToAction(nameof(ViewList), new { id = list.Id });
        }

        [HttpPost]
        public async Task<IActionResult> RefreshAisles(int id, string storeId)
        {
            var list = await _shoppingListService.GetByIdAsync(id);
            if (list == null) return NotFound();

            var upcItems = list.Items.Where(i => !string.IsNullOrWhiteSpace(i.Upc)).ToList();
            if (!upcItems.Any())
                return Ok();

            var products = await _krogerService.GetProductsByUpcBatch(upcItems.Select(i => i.Upc!), storeId);

            var updates = new Dictionary<int, (string? aisleNumber, string? aisleDescription)>();
            foreach (var item in upcItems)
            {
                if (products.TryGetValue(item.Upc!, out var product))
                {
                    updates[item.Id] = (
                        product.aisleLocation != "N/A" ? product.aisleLocation : null,
                        product.aisleDescription
                    );
                }
            }

            await _shoppingListService.UpdateItemAislesAsync(id, storeId, updates);
            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _shoppingListService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMultiple(List<int> ids)
        {
            foreach (var id in ids)
                await _shoppingListService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAll()
        {
            await _shoppingListService.DeleteAllAsync();
            return RedirectToAction(nameof(Index));
        }

        private static string? InferSectionFromCategories(List<string>? categories)
        {
            if (categories == null || categories.Count == 0) return null;
            var joined = string.Join(" ", categories);
            if (joined.Contains("produce", StringComparison.OrdinalIgnoreCase) ||
                joined.Contains("vegetable", StringComparison.OrdinalIgnoreCase) ||
                joined.Contains("fruit", StringComparison.OrdinalIgnoreCase))
                return "PRODUCE";
            if (joined.Contains("meat", StringComparison.OrdinalIgnoreCase) ||
                joined.Contains("seafood", StringComparison.OrdinalIgnoreCase) ||
                joined.Contains("poultry", StringComparison.OrdinalIgnoreCase))
                return "MEAT";
            if (joined.Contains("dairy", StringComparison.OrdinalIgnoreCase) ||
                joined.Contains("cheese", StringComparison.OrdinalIgnoreCase) ||
                joined.Contains("egg", StringComparison.OrdinalIgnoreCase))
                return "DAIRY";
            return null;
        }
    }
}
