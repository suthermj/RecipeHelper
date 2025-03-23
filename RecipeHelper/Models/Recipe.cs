using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace RecipeHelper.Models
{
    public class Recipe
    {
        [Key]
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? ImageUri { get; set; } = string.Empty;
        public List<RecipeProduct> RecipeProducts { get; set; } = [];
    }
    
    public class ViewRecipeVM
    {
        public int Id { get; set; }
        //[JsonPropertyName("recipeName")]
        public required string RecipeName { get; set; }
        //[JsonPropertyName("imageUri")]
        public string ImageUri { get; set; } = string.Empty;
        //[JsonPropertyName("recipeProducts")]
        public List<IngredientNameVM> Ingredients { get; set; } = [];
    }

    public class CreateRecipeVM
    {
        //[JsonPropertyName("recipeName")]
        public required string RecipeName { get; set; }
        //[JsonPropertyName("imageUri")]
        public IFormFile? ImageFile { get; set; }

        //[JsonPropertyName("recipeProducts")]
        public List<IngredientIdVM> Ingredients { get; set; } = [];
    }

    public class IngredientIdVM
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
    }

    public class IngredientNameVM
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
    }
}
