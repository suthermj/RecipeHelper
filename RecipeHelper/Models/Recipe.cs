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
        public string? SelectedKrogerName { get; set; }    // display hint only, not persisted
        public int IngredientId { get; set; }              // FK (re-resolved on save)
        public string? Section { get; set; }
        public bool IsModified { get; set; }
    }

    public class IngredientVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
        // The ingredient line as written (RecipeIngredient.OriginalText); null for
        // ingredients saved before that existed.
        public string? Text { get; set; }
        public string? Section { get; set; }
        public decimal Quantity { get; set; }
        public string Measurement { get; set; }
        public string Upc { get; set; }

        // False for "to taste" / "as needed" ingredients, stored with Quantity 0.
        public bool HasAmount => Quantity != 0;

        // The two columns the recipe page shows: amount ("1/2 Cups") and name. Taken from
        // the line as written when there is one, else built from Quantity/Measurement.
        public string AmountLabel => string.IsNullOrWhiteSpace(Text)
            ? $"{DisplayQuantity} {DisplayMeasurement}".Trim()
            : IngredientLineSplitter.Split(Text).Amount;
        public string NameLabel => string.IsNullOrWhiteSpace(Text)
            ? Name
            : IngredientLineSplitter.Split(Text).Name;

        public string DisplayQuantity       // The property name you use in Razor
        {
            get                             // Computed getter
            {
                if (!HasAmount) return "";

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
                if (!HasAmount) return "";
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

        // Identifies which canonical (merged) row on the ingredient review page this
        // ingredient belongs to -- name + dimension family, not name + exact measurement
        // string. DinnerController.SubmitDinnerSelections can emit up to 3 rows for one
        // ingredient name when merged entries span dimensions (volume/weight/unit), and
        // the "best display unit" it then picks for a merged row (e.g. summed
        // tablespoons re-expressed as cups) need not match any single recipe's original
        // measurement string. Bucketing by dimension family instead of the literal
        // string is what lets the by-recipe view (#78) and the merged section view stay
        // in sync when toggling the same ingredient from either one. Count and Unknown
        // both bucket as "Unit", matching the `default:` case in that same aggregation.
        //
        // The identity half is GroupKey when set (see below), else the normalized name.
        public string DimensionKey
        {
            get
            {
                var dim = UnitConverter.GetDimension(UnitConverter.Parse(Measurement));
                var bucket = dim switch
                {
                    MeasureDimension.Volume => "Volume",
                    MeasureDimension.Weight => "Weight",
                    _ => "Unit"
                };
                return $"{GroupKey ?? Name?.Trim().ToLowerInvariant()}|{bucket}";
            }
        }

        // Which merged row this ingredient belongs to on the review page, set by
        // DinnerController.SubmitDinnerSelections. It's the linked Kroger UPC when there
        // is one, so differently-worded ingredients that buy the same product
        // ("Garlic Cloves (Minced)", "garlic minced", "garlic, minced") merge into one
        // row instead of one row -- and one toggle -- per wording. Unlinked ingredients
        // fall back to their normalized name.
        public string? GroupKey { get; set; }

        // Matches the user's pantry list (PantryMatcher); the review page's "Uncheck
        // pantry" button unchecks these rows.
        public bool IsPantry { get; set; }
    }
}
