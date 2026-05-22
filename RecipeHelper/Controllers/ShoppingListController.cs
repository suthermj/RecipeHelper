using Microsoft.AspNetCore.Mvc;
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

            var upcs = includedItems.Where(i => !string.IsNullOrWhiteSpace(i.Upc)).Select(i => i.Upc!);
            var productMap = await _krogerService.GetProductsByUpcBatch(upcs, storeId);

            var items = includedItems.Select(i =>
            {
                var item = new ShoppingListItem
                {
                    Name = i.Name,
                    Quantity = (int)Math.Ceiling(i.Quantity),
                    Upc = i.Upc
                };

                if (i.Upc != null && productMap.TryGetValue(i.Upc, out var dto))
                {
                    item.AisleNumber = dto.aisleLocation != "N/A" ? dto.aisleLocation : null;
                    item.AisleDescription = dto.aisleDescription ?? InferSectionFromCategories(dto.categories);
                    item.Brand = dto.brand;
                    item.Price = dto.regularPrice > 0 ? (decimal?)dto.regularPrice : null;
                    item.PromoPrice = dto.promoPrice > 0 ? (decimal?)dto.promoPrice : null;
                }

                return item;
            }).ToList();

            var name = $"Meal Plan – {DateTime.Now:MMM d, yyyy}";
            var list = await _shoppingListService.CreateAsync(name, items, storeId);

            return RedirectToAction(nameof(ViewList), new { id = list.Id });
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
