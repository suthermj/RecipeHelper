using Microsoft.EntityFrameworkCore;
using RecipeHelper.Models.Dinner;
using System.Security.Cryptography;

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
            _logger.LogInformation("Meal plan saved. WeekStart={WeekStart}, PlanId={PlanId}, EntryCount={EntryCount}", weekStart, existing.Id, existing.Entries.Count);
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
                _logger.LogInformation("AddEntryAsync: RecipeId={RecipeId} already on PlanId={PlanId} for DayOfWeek={DayOfWeek}, skipping duplicate add.", recipeId, plan.Id, dayOfWeek);
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
            _logger.LogInformation("AddEntryAsync: added RecipeId={RecipeId} to PlanId={PlanId} for DayOfWeek={DayOfWeek}.", recipeId, plan.Id, dayOfWeek);

            return await _context.MealPlans
                .Include(p => p.Entries)
                    .ThenInclude(e => e.Recipe)
                .FirstAsync(p => p.Id == plan.Id);
        }

        // Appends a free-text (non-recipe) placeholder entry for the given day, e.g.
        // "homemade pizzas" when nothing's been imported for it yet. Creates the plan
        // on first entry, same as AddEntryAsync. Not deduped -- unlike recipes, the
        // same free-text label is a reasonable thing to plan for more than one day.
        public async Task<MealPlan> AddFreeTextEntryAsync(DateTime weekStart, int dayOfWeek, string text)
        {
            if (dayOfWeek < 0 || dayOfWeek > 6)
                throw new ArgumentOutOfRangeException(nameof(dayOfWeek));

            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text is required.", nameof(text));

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

            plan.Entries.Add(new MealPlanEntry
            {
                MealPlanId = plan.Id,
                FreeText = text.Trim(),
                DayOfWeek = dayOfWeek
            });

            await _context.SaveChangesAsync();
            _logger.LogInformation("AddFreeTextEntryAsync: added free-text entry to PlanId={PlanId} for DayOfWeek={DayOfWeek}.", plan.Id, dayOfWeek);

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

            if (entry == null)
            {
                _logger.LogWarning("MoveEntryAsync: no MealPlanEntry found for EntryId={EntryId}.", entryId);
                return null;
            }

            var previousDayOfWeek = entry.DayOfWeek;
            entry.DayOfWeek = dayOfWeek;
            await _context.SaveChangesAsync();
            _logger.LogInformation("MoveEntryAsync: EntryId={EntryId} moved from DayOfWeek={PreviousDayOfWeek} to DayOfWeek={NewDayOfWeek}.", entryId, previousDayOfWeek, dayOfWeek);

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

            if (entry == null)
            {
                _logger.LogWarning("RemoveEntryAsync: no MealPlanEntry found for EntryId={EntryId}.", entryId);
                return null;
            }

            var plan = entry.MealPlan;
            _context.MealPlanEntries.Remove(entry);
            plan.Entries.Remove(entry);

            if (plan.Entries.Count == 0)
            {
                _context.MealPlans.Remove(plan);
                await _context.SaveChangesAsync();
                _logger.LogInformation("RemoveEntryAsync: removed EntryId={EntryId}, last entry on PlanId={PlanId} -- plan deleted.", entryId, plan.Id);
                return null;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("RemoveEntryAsync: removed EntryId={EntryId} from PlanId={PlanId}, {RemainingCount} entries remain.", entryId, plan.Id, plan.Entries.Count);

            return await _context.MealPlans
                .Include(p => p.Entries)
                    .ThenInclude(e => e.Recipe)
                .FirstAsync(p => p.Id == plan.Id);
        }

        // Returns the plan's existing share token, generating and persisting one on first call.
        public async Task<string?> GetOrCreateShareTokenAsync(int planId)
        {
            var plan = await _context.MealPlans.FirstOrDefaultAsync(p => p.Id == planId);
            if (plan == null) return null;

            if (string.IsNullOrEmpty(plan.ShareToken))
            {
                plan.ShareToken = GenerateShareToken();
                await _context.SaveChangesAsync();
            }

            return plan.ShareToken;
        }

        public async Task<MealPlan?> GetByShareTokenAsync(string token)
        {
            return await _context.MealPlans
                .Include(p => p.Entries)
                    .ThenInclude(e => e.Recipe)
                .FirstOrDefaultAsync(p => p.ShareToken == token);
        }

        private static string GenerateShareToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
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
                _logger.LogInformation("DeleteAsync: deleted PlanId={PlanId} (WeekStart={WeekStart}, {EntryCount} entries).", id, plan.WeekStartDate, plan.Entries.Count);
            }
            else
            {
                _logger.LogWarning("DeleteAsync: no MealPlan found for PlanId={PlanId}.", id);
            }
        }
    }
}
