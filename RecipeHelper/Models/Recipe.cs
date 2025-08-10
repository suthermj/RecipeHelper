using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

    public class DraftRecipe
    {
        [Key]
        public int Id { get; set; }

        // Nullable to accommodate drafts without a corresponding published recipe
        [ForeignKey("PublishedRecipe")]
        public int? PublishedRecipeId { get; set; }

        [Required]
        public string Name { get; set; }

        public string? ImageUri { get; set; } = string.Empty;
        //public List<RecipeProduct> RecipeProducts { get; set; } = new List<RecipeProduct>();

        // Navigation property for the published recipe
        //public virtual Recipe? PublishedRecipe { get; set; }
    }


    public class ViewRecipeVM
    {
        public int Id { get; set; }
        public required string RecipeName { get; set; }
        public string ImageUri { get; set; } = string.Empty;
        public List<IngredientVM> Ingredients { get; set; } = [];
    }

    public class SubmitDinnerSelectionsVM
    {
        public List<SelectedRecipeVM> SelectedRecipes { get; set; }
        public Dictionary<string, int> Ingredients { get; set; }
    }

    public class SelectedRecipeVM
    {
        public string RecipeName { get; set; }
        public string ImageUri { get; set; }
    }
    public class CreateRecipeVM
    {
        public int recipeId { get; set; }
        public string recipeName { get; set; }
        public IFormFile? imageFile { get; set; }
        public string? imageUri { get; set; }
        public List<IngredientVM> ingredients { get; set; } = [];

        public bool modifying { get; set; } = false;
    }

    public class ModifyIngredientsVM
    {
        public int RecipeId { get; set; }
        public int publishedRecipeId { get; set; }
        public List<ProductVM> CurrentIngredients { get; set; } = new();
        public List<ProductVM> AllProducts { get; set; } = new();
    }

    public class IngredientVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
    }
}
