using Microsoft.AspNetCore.Mvc;
using RecipeHelper.Models.Lists;
using RecipeHelper.Services;

namespace RecipeHelper.Controllers
{
    public class ShoppingListController : Controller
    {
        private readonly ILogger<ShoppingListController> _logger;
        private readonly ShoppingListService _shoppingListService;

        public ShoppingListController(ILogger<ShoppingListController> logger, ShoppingListService shoppingListService)
        {
            _logger = logger;
            _shoppingListService = shoppingListService;
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
            var items = model.Items
                .Where(i => i.Include)
                .Select(i => new ShoppingListItem
                {
                    Name = i.Name,
                    Quantity = (int)Math.Ceiling(i.Quantity),
                    Upc = i.Upc
                })
                .ToList();

            var name = $"Meal Plan – {DateTime.Now:MMM d, yyyy}";
            var list = await _shoppingListService.CreateAsync(name, items);

            return RedirectToAction(nameof(ViewList), new { id = list.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            await _shoppingListService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
