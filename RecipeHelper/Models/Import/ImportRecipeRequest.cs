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
        public decimal Amount { get; set; }
        public string? Unit { get; set; }
        public bool Include { get; set; }
        public int? ProductId { get; set; }
        public bool UseKroger { get; set; }
        public string? KrogerUpc { get; set; }
    }
}
