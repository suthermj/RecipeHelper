using Newtonsoft.Json;

namespace RecipeHelper.Models.IngredientModels
{
    public class CreateIngredientRequest
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
