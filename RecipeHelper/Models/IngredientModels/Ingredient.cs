using System.Text.Json.Serialization;

namespace RecipeHelper.Models.IngredientModels
{
    public class Ingredient
    {
        public int Id { get; set; }

        // Stable matching key (e.g. spoonacular nameClean/name)
        public string CanonicalName { get; set; } = null!; // "bacon", "yellow onion"

        // Optional: nice label if you ever want it
        public string? DefaultDisplayName { get; set; }

        public List<IngredientKrogerProduct> KrogerMappings { get; set; } = new();
        public List<RecipeIngredient> RecipeIngredients { get; set; } = new();
    }

    // OpenAI canonicalization result
    public sealed class CanonicalizeResult
    {
        [JsonPropertyName("canonical_name")]
        public string CanonicalName { get; set; } = "";
        public decimal Confidence { get; init; }
    }
}
