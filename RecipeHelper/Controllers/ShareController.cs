using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RecipeHelper.Models.Dinner;
using RecipeHelper.Services;

namespace RecipeHelper.Controllers
{
    // Public by design: share links are opened by people without an account.
    [AllowAnonymous]
    public class ShareController : Controller
    {
        private readonly MealPlanService _mealPlanService;

        public ShareController(MealPlanService mealPlanService)
        {
            _mealPlanService = mealPlanService;
        }

        // GET: Share/MealPlan/{token} — public, read-only week view
        [HttpGet("Share/MealPlan/{token}")]
        public async Task<IActionResult> MealPlan(string token)
        {
            var plan = await _mealPlanService.GetByShareTokenAsync(token);
            if (plan == null) return NotFound();

            var vm = new ShareMealPlanVM
            {
                WeekStart = plan.WeekStartDate,
                Plan = plan,
            };

            return View(vm);
        }
    }
}
