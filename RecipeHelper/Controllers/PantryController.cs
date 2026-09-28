using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models;
using RecipeHelper.Utility;

namespace RecipeHelper.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class PantryController : Controller
    {
        private readonly DatabaseContext _context;
        private readonly ILogger<PantryController> _logger;

        public PantryController(DatabaseContext context, ILogger<PantryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Add(string name)
        {
            var trimmed = (name ?? "").Trim();
            var normalized = PantryMatcher.Normalize(trimmed);
            if (normalized.Length == 0)
                return RedirectToAction("Index", "Settings", new { pantryOpen = 1 });

            if (await _context.PantryItems.AnyAsync(p => p.NormalizedName == normalized))
            {
                TempData["SuccessMessage"] = $"{trimmed} is already in your pantry";
                return RedirectToAction("Index", "Settings", new { pantryOpen = 1 });
            }

            _context.PantryItems.Add(new PantryItem { Name = trimmed, NormalizedName = normalized });
            await _context.SaveChangesAsync();
            _logger.LogInformation("Pantry item added. Name={Name}", trimmed);
            TempData["SuccessMessage"] = $"Added {trimmed} to your pantry";
            return RedirectToAction("Index", "Settings", new { pantryOpen = 1 });
        }

        [HttpPost]
        public async Task<IActionResult> Remove(int id)
        {
            var item = await _context.PantryItems.FindAsync(id);
            if (item != null)
            {
                _context.PantryItems.Remove(item);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Pantry item removed. Name={Name}", item.Name);
            }
            return RedirectToAction("Index", "Settings", new { pantryOpen = 1 });
        }
    }
}
