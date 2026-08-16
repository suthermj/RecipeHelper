namespace RecipeHelper.Models.Dinner
{
    public class SubmitDinnerSelectionsVM
    {
        public List<SelectedRecipeVM> SelectedRecipes { get; set; }
        public Dictionary<string, int> Ingredients { get; set; }
    }

    public class ReviewDinnerSelectionsVM
    {
        public List<SelectedRecipeVM> SelectedRecipes { get; set; }
        public List<IngredientVM> Ingredients { get; set; }
        public List<RecipeIngredientGroupVM> RecipeGroups { get; set; } = new();
    }

    public class SelectedRecipeVM
    {
        public string RecipeName { get; set; }
        public string ImageUri { get; set; }
    }

    // Per-recipe view of the same ingredients that get merged into
    // ReviewDinnerSelectionsVM.Ingredients -- built alongside that merge, not from it,
    // since attribution to a specific recipe is lost once ingredients are summed by
    // name. Used only for the by-recipe display grouping on the review page; the merged
    // Ingredients list stays the sole source of truth for submitted quantities.
    public class RecipeIngredientGroupVM
    {
        public string RecipeName { get; set; }
        public string ImageUri { get; set; }
        public int Occurrences { get; set; } // recipe on 2 days => 2
        public List<IngredientVM> Ingredients { get; set; } = new();
    }
}
