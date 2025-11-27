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
    }

    public class SelectedRecipeVM
    {
        public string RecipeName { get; set; }
        public string ImageUri { get; set; }
    }
}
