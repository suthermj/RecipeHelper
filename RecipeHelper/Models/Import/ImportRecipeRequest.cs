using System.ComponentModel.DataAnnotations;

namespace RecipeHelper.Models.Import
{
    public class ImportRecipeRequest
    {
        [Required]
        public string Title { get; set; } = "";

        public string? Image { get; set; }

        [MinLength(1)]
        public List<ImportedIngredient> Ingredients { get; set; } = new();
    }

    public class ImportedIngredient
    {
        [Required]
        public string Name { get; set; } = "";
        public string CanonicalName { get; set; } = "";
        public int? IngredientId { get; set; }
        public decimal Amount { get; set; }
        public string? Unit { get; set; }
        public bool Include { get; set; }// ✅ Final chosen selection (what gets saved)
        public string Upc { get; set; }
        public string? SelectedName { get; set; }
        public string? SelectedSource { get; set; }         // "Suggested", "Kroger", "Unselected"
    }
}
