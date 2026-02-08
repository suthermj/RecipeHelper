using RecipeHelper.Models.Kroger;

namespace RecipeHelper.Models.IngredientModels
{
    public class IngredientKrogerProduct
    {
        public int IngredientId { get; set; }
        public Ingredient Ingredient { get; set; } = null!;

        public string Upc { get; set; } = null!;
        public KrogerProduct KrogerProduct { get; set; } = null!;

        public decimal? Confidence { get; set; } // optional
        public bool IsDefault { get; set; }      // optional
        public string? MatchMethod { get; set; } // "Exact", "Fuzzy", "UserSelected", "LLM"
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
