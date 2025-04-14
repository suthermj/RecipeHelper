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
        public int Id { get; set; }
        public string RecipeName { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string? ImageUri { get; set; }
        public List<IngredientVM> Ingredients { get; set; } = [];

        public bool Modifying { get; set; } = false;
    }

    public class IngredientVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
    }
}
