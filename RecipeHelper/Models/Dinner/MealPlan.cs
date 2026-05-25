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

    public class MealPlanIndexVM
    {
        public DateTime WeekStart { get; set; }
        public MealPlan? Plan { get; set; }
        public List<ViewRecipeVM> AllRecipes { get; set; } = new();
    }
}
