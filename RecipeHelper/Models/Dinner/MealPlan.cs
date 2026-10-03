using RecipeHelper.Models;

namespace RecipeHelper.Models.Dinner
{
    public class MealPlan
    {
        public int Id { get; set; }
        public DateTime WeekStartDate { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string? ShareToken { get; set; }
        public List<MealPlanEntry> Entries { get; set; } = new();
    }

    public class MealPlanEntry
    {
        public int Id { get; set; }
        public int MealPlanId { get; set; }
        public MealPlan MealPlan { get; set; } = null!;
        public int? RecipeId { get; set; }
        public Recipe? Recipe { get; set; }
        // Set instead of RecipeId/Recipe for a plain "what's for dinner" placeholder
        // that isn't backed by an imported recipe (e.g. "homemade pizzas").
        public string? FreeText { get; set; }
        public int DayOfWeek { get; set; }   // 0 = Monday … 6 = Sunday
    }

    public class MealPlanIndexVM
    {
        public DateTime WeekStart { get; set; }
        public MealPlan? Plan { get; set; }
        public List<ViewRecipeVM> AllRecipes { get; set; } = new();
    }

    // Intermediate step between the meal plan and ingredient review (Dinner/SelectIngredientRecipes)
    // -- lets the user exclude recipes they don't need groceries for this round (e.g. a recipe
    // carried over unchanged from last week that they already bought ingredients for).
    public class SelectIngredientRecipesVM
    {
        public DateTime WeekStart { get; set; }
        public List<IngredientRecipeSelectionVM> Recipes { get; set; } = new();
    }

    public class IngredientRecipeSelectionVM
    {
        public int RecipeId { get; set; }
        public string RecipeName { get; set; } = "";
        public string? ImageUri { get; set; }
        public string? ThumbnailUri { get; set; }
        public int Occurrences { get; set; } // recipe on 2 days => 2
        public List<string> Days { get; set; } = new();
    }

    public class ShareMealPlanVM
    {
        public DateTime WeekStart { get; set; }
        public MealPlan Plan { get; set; } = null!;
    }
}
