using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Rendering;
using RecipeHelper.Utility;

namespace RecipeHelper.Models
{
    public class Recipe
    {
        [Key]
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? ImageUri { get; set; } = string.Empty;
        // Small (~500px max dimension) derivative of ImageUri, used anywhere the image
        // renders as a thumbnail (meal plan entries, recipe picker, recipe list) instead
        // of transferring the full-size image for a tiny box. Null for recipes whose
        // image was uploaded before this existed, or if thumbnail generation failed --
        // callers fall back to ImageUri in that case.
        public string? ThumbnailUri { get; set; }
        public string? Instructions { get; set; }
        public string? DinnerCategory { get; set; }
        public string? SourceUrl { get; set; }
        public List<RecipeIngredient> Ingredients { get; set; } = [];
    }

    public class ViewRecipeVM
    {
        public int Id { get; set; }
        public required string RecipeName { get; set; }
        public string ImageUri { get; set; } = string.Empty;
        public string? ThumbnailUri { get; set; }
        public string? DinnerCategory { get; set; }
        public string? SourceUrl { get; set; }
        public List<IngredientVM> Ingredients { get; set; } = [];
        public List<string> Instructions { get; set; } = new();
    }

    public class CreateRecipeVM
    {
        public string Title { get; set; }
        public string? DinnerCategory { get; set; }
        public IFormFile? ImageFile { get; set; }
        public List<CreateRecipeIngredientVM> Ingredients { get; set; } = new();
        public List<string> Instructions { get; set; } = new();
    }

    public class EditRecipeVM
    {
        public int RecipeId { get; set; }
        public string Title { get; set; }
        public string? DinnerCategory { get; set; }
        public string? ImageUri { get; set; }
        public string? ThumbnailUri { get; set; }
        public IFormFile? ImageFile { get; set; }
        public List<EditRecipeIngredientVM> Ingredients { get; set; } = new();
        public List<string> Instructions { get; set; } = new();
    }

    public class CreateRecipeIngredientVM
    {
        public string RawText { get; set; } = "";          // e.g. "2 cups flour"
        public string? SelectedKrogerUpc { get; set; }     // optional Kroger link
        public string? Section { get; set; }
    }

    public class EditRecipeIngredientVM
    {
        public int Id { get; set; }                        // RecipeIngredient PK (0 = new)
        public string RawText { get; set; } = "";          // e.g. "2 cups flour"
        public string? SelectedKrogerUpc { get; set; }     // optional Kroger link
        public int IngredientId { get; set; }              // FK (re-resolved on save)
        public string? Section { get; set; }
        public bool IsModified { get; set; }
    }

    public class IngredientVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string? Section { get; set; }
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

                // Otherwise display as a fraction (ex: 0.33 → 1/3, 1.5 → 1 1/2)
                return UnitConverter.ToFractionString(Quantity);
            }
        }
        public string DisplayMeasurement    // The property name you use in Razor
        {
            get                             // Computed getter
            {
                if (Quantity == 1 && Measurement?.Equals("Unit", StringComparison.OrdinalIgnoreCase) == false)
                {
                    // Handle compound units like "Fluid Ounces" → "Fluid Ounce"
                    if (Measurement.Contains(' '))
                    {
                        var lastSpace = Measurement.LastIndexOf(' ');
                        var lastWord = Measurement[(lastSpace + 1)..];
                        return Measurement[..lastSpace] + " " + lastWord[..^1];
                    }
                    return Measurement[..^1];
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
