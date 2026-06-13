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

        private static readonly TimeZoneInfo UserTimeZone = ResolveUserTimeZone();

        private static TimeZoneInfo ResolveUserTimeZone()
        {
            // IANA id works cross-platform on .NET 6+ (Windows uses ICU).
            try { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
        }

        public static DateTime LocalToday()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, UserTimeZone).Date;
        }

        public static DateTime GetWeekStart(DateTime date)
        {
            int offset = ((int)date.DayOfWeek - 1 + 7) % 7;
            return date.Date.AddDays(-offset);
        }

        public async Task<MealPlan?> GetByWeekAsync(DateTime weekStart)
        {
            return await _context.MealPlans
                .Include(p => p.Entries)
                    .ThenInclude(e => e.Recipe)
                .FirstOrDefaultAsync(p => p.WeekStartDate == weekStart);
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

        // Appends a new entry for the given day. Creates the plan on first entry.
        public async Task<MealPlan> AddEntryAsync(DateTime weekStart, int dayOfWeek, int recipeId)
        {
            if (dayOfWeek < 0 || dayOfWeek > 6)
                throw new ArgumentOutOfRangeException(nameof(dayOfWeek));

            var plan = await _context.MealPlans
                .Include(p => p.Entries)
                .FirstOrDefaultAsync(p => p.WeekStartDate == weekStart);

            if (plan == null)
            {
                plan = new MealPlan
                {
                    WeekStartDate = weekStart,
                    CreatedUtc = DateTime.UtcNow
                };
                _context.MealPlans.Add(plan);
                await _context.SaveChangesAsync();
            }

            if (plan.Entries.Any(e => e.RecipeId == recipeId))
            {
                return await _context.MealPlans
                    .Include(p => p.Entries)
                        .ThenInclude(e => e.Recipe)
                    .FirstAsync(p => p.Id == plan.Id);
            }

            plan.Entries.Add(new MealPlanEntry
            {
                MealPlanId = plan.Id,
                RecipeId = recipeId,
                DayOfWeek = dayOfWeek
            });

            await _context.SaveChangesAsync();

            return await _context.MealPlans
                .Include(p => p.Entries)
                    .ThenInclude(e => e.Recipe)
                .FirstAsync(p => p.Id == plan.Id);
        }

        public async Task<MealPlan?> MoveEntryAsync(int entryId, int dayOfWeek)
        {
            if (dayOfWeek < 0 || dayOfWeek > 6)
                throw new ArgumentOutOfRangeException(nameof(dayOfWeek));

            var entry = await _context.MealPlanEntries
                .Include(e => e.MealPlan)
                .FirstOrDefaultAsync(e => e.Id == entryId);

            if (entry == null) return null;

            entry.DayOfWeek = dayOfWeek;
            await _context.SaveChangesAsync();

            return await _context.MealPlans
                .Include(p => p.Entries)
                    .ThenInclude(e => e.Recipe)
                .FirstAsync(p => p.Id == entry.MealPlanId);
        }

        // Removes a single entry by id. Deletes the plan when the last entry is cleared.
        // Returns the plan after removal, or null if the plan was deleted.
        public async Task<MealPlan?> RemoveEntryAsync(int entryId)
        {
            var entry = await _context.MealPlanEntries
                .Include(e => e.MealPlan)
                    .ThenInclude(p => p.Entries)
                .FirstOrDefaultAsync(e => e.Id == entryId);

            if (entry == null) return null;

            var plan = entry.MealPlan;
            _context.MealPlanEntries.Remove(entry);
            plan.Entries.Remove(entry);

            if (plan.Entries.Count == 0)
            {
                _context.MealPlans.Remove(plan);
                await _context.SaveChangesAsync();
                return null;
            }

            await _context.SaveChangesAsync();

            return await _context.MealPlans
                .Include(p => p.Entries)
                    .ThenInclude(e => e.Recipe)
                .FirstAsync(p => p.Id == plan.Id);
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
