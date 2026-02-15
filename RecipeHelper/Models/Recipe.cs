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
        public string? Instructions { get; set; }
        public List<RecipeIngredient> Ingredients { get; set; } = [];
    }

    public class ViewRecipeVM
    {
        public int Id { get; set; }
        public required string RecipeName { get; set; }
        public string ImageUri { get; set; } = string.Empty;
        public List<IngredientVM> Ingredients { get; set; } = [];
        public List<string> Instructions { get; set; } = new();
    }

    public class CreateRecipeVM
    {
        public string Title { get; set; }
        public IFormFile? ImageFile { get; set; }
        public List<CreateRecipeIngredientVM> Ingredients { get; set; } = new();
        public List<string> Instructions { get; set; } = new();
    }

    public class EditRecipeVM
    {
        public int RecipeId { get; set; }
        public string Title { get; set; }
        public string? ImageUri { get; set; }
        public IFormFile? ImageFile { get; set; }
        public List<EditRecipeIngredientVM> Ingredients { get; set; } = new();
        public List<string> Instructions { get; set; } = new();
    }

    public class CreateRecipeIngredientVM
    {
        public string DisplayName { get; set; } = "";   // required
        public decimal Quantity { get; set; }           // required
        public int? MeasurementId { get; set; }         // required-ish (or default “Count”)
        public string? SelectedKrogerUpc { get; set; }  // optional
    }

    public class EditRecipeIngredientVM
    {
        public int Id { get; set; }                     // RecipeIngredient PK (0 = new row)
        public string DisplayName { get; set; } = "";   // required
        public decimal Quantity { get; set; }           // required
        public int? MeasurementId { get; set; }         // required-ish (or default "Count")
        public string? SelectedKrogerUpc { get; set; }  // optional
        public int IngredientId { get; set; }           // FK to Ingredient table
    }

    public class IngredientVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public string Measurement { get; set; }
        public string Upc { get; set; }
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
                else if (Measurement?.Equals("Unit", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return "";
                }
                return Measurement;
            }
        }
    }
}
