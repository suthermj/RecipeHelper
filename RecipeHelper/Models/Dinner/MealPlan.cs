using RecipeHelper.Models;

namespace RecipeHelper.Models.Dinner
{
    public class MealPlan
    {
        public int Id { get; set; }
        public DateTime WeekStartDate { get; set; }
        public DateTime CreatedUtc { get; set; }
        public List<MealPlanEntry> Entries { get; set; } = new();
    }

    public class MealPlanEntry
    {
        public int Id { get; set; }
        public int MealPlanId { get; set; }
        public MealPlan MealPlan { get; set; } = null!;
        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;
        public int DayOfWeek { get; set; }   // 0 = Monday … 6 = Sunday
    }

    public class SaveMealPlanVM
    {
        public DateTime WeekStartDate { get; set; }
        public int?[] DayRecipes { get; set; } = new int?[7];
    }

    public class PlanWeekVM
    {
        public DateTime WeekStartDate { get; set; }
        public List<ViewRecipeVM> AllRecipes { get; set; } = new();
        public int?[] DayRecipes { get; set; } = new int?[7];
    }
}
