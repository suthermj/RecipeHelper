namespace RecipeHelper.Models.Import
{
    public class ImportRecipeResponse
    {
        public int RecipeId { get; set; }
        public bool Success { get; set; } = false;
        public string? ErrorMessage { get; set; }
    }
}
