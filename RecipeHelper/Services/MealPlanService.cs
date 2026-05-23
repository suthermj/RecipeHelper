using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models.Dinner;

namespace RecipeHelper.Services
{
    public class MealPlanService
    {
        private readonly ILogger<MealPlanService> _logger;
        private readonly DatabaseContext _context;

        public MealPlanService(ILogger<MealPlanService> logger, DatabaseContext context)
        {
            _logger = logger;
            _context = context;
        }

        public static DateTime GetWeekStart(DateTime date)
        {
            int offset = ((int)date.DayOfWeek - 1 + 7) % 7;
            return date.Date.AddDays(-offset);
        }

        public async Task<MealPlan?> GetCurrentWeekAsync()
        {
            var weekStart = GetWeekStart(DateTime.UtcNow);
            return await _context.MealPlans
                .Include(p => p.Entries)
                    .ThenInclude(e => e.Recipe)
                .FirstOrDefaultAsync(p => p.WeekStartDate == weekStart);
        }

        public async Task<List<MealPlan>> GetHistoryAsync()
        {
            return await _context.MealPlans
                .Include(p => p.Entries)
                    .ThenInclude(e => e.Recipe)
                .OrderByDescending(p => p.WeekStartDate)
                .ToListAsync();
        }

        public async Task<MealPlan?> GetByIdAsync(int id)
        {
            return await _context.MealPlans
                .Include(p => p.Entries)
                    .ThenInclude(e => e.Recipe)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<MealPlan> SaveAsync(DateTime weekStart, int?[] dayRecipes)
        {
            var existing = await _context.MealPlans
                .Include(p => p.Entries)
                .FirstOrDefaultAsync(p => p.WeekStartDate == weekStart);

            if (existing != null)
            {
                _context.MealPlanEntries.RemoveRange(existing.Entries);
                existing.Entries.Clear();
            }
            else
            {
                existing = new MealPlan
                {
                    WeekStartDate = weekStart,
                    CreatedUtc = DateTime.UtcNow
                };
                _context.MealPlans.Add(existing);
                await _context.SaveChangesAsync();
            }

            for (int i = 0; i < dayRecipes.Length; i++)
            {
                if (dayRecipes[i].HasValue)
                {
                    existing.Entries.Add(new MealPlanEntry
                    {
                        MealPlanId = existing.Id,
                        RecipeId = dayRecipes[i]!.Value,
                        DayOfWeek = i
                    });
                }
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task DeleteAsync(int id)
        {
            var plan = await _context.MealPlans
                .Include(p => p.Entries)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (plan != null)
            {
                _context.MealPlans.Remove(plan);
                await _context.SaveChangesAsync();
            }
        }
    }
}
