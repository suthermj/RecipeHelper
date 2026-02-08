using System.Text.Json;
using System.Text.Json.Serialization;

namespace RecipeHelper.Models.Spoonacular
{
    public class JsonLdRecipe
    {
        [JsonPropertyName("@type")]
        public string? Type { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("image")]
        public object? Image { get; set; } // string | array

        [JsonPropertyName("recipeYield")]
        public object? RecipeYield { get; set; } // string | array

        [JsonPropertyName("prepTime")]
        public string? PrepTime { get; set; }

        [JsonPropertyName("cookTime")]
        public string? CookTime { get; set; }

        [JsonPropertyName("totalTime")]
        public string? TotalTime { get; set; }

        [JsonPropertyName("recipeIngredient")]
        public List<string>? RecipeIngredient { get; set; }

        [JsonPropertyName("recipeInstructions")]
        public List<JsonLdHowToStep>? RecipeInstructions { get; set; }

        [JsonPropertyName("recipeCategory")]
        public List<string>? RecipeCategory { get; set; }

        [JsonPropertyName("recipeCuisine")]
        public List<string>? RecipeCuisine { get; set; }

        [JsonPropertyName("keywords")]
        public string? Keywords { get; set; }

        public string? ExtractFirstImage()
        {
            return Image switch
            {
                string s => s,
                JsonElement e when e.ValueKind == JsonValueKind.Array
                    => e.EnumerateArray().FirstOrDefault().GetString(),
                _ => null
            };
        }
    }

    public class JsonLdHowToStep
    {
        [JsonPropertyName("@type")]
        public string? Type { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
