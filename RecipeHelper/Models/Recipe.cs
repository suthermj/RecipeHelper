using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Rendering;

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
        public IEnumerable<SelectListItem> AvailableMeasurements { get; set; }
    }

    public class IngredientVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public string Measurement { get; set; }
        public string DisplayQuantity       // The property name you use in Razor
        {
            get                             // Computed getter
            {
                // If the measurement is Unit, we want whole numbers (ex: 2.00 → 2)
                if (Measurement?.Equals("Unit", StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (Quantity % 1 == 0)  // means it's a whole number (ex: 1.00, 2.00)
                        return ((int)Quantity).ToString();
                }

                // Otherwise trim trailing zeros (ex: 1.50 → 1.5, 2.00 → 2)
                return Quantity.ToString("0.##");
            }
        }
        public string DisplayMeasurement    // The property name you use in Razor
        {
            get                             // Computed getter
            {
                if (Quantity == 1 && Measurement?.Equals("Unit", StringComparison.OrdinalIgnoreCase) == false)
                {
                    return Measurement.Substring(0, Measurement.Length - 1);
                }
                return Measurement;
            }
        }
    }
}
