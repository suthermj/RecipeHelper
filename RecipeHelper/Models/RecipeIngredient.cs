using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Permissions;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Models.IngredientModels;

namespace RecipeHelper.Models
{
    public class RecipeIngredient
    {
        [Key]
        public int Id { get; set; }

        public int RecipeId { get; set; }
        public Recipe Recipe { get; set; } = null!;

        public int IngredientId { get; set; }
        public Ingredient Ingredient { get; set; } = null!;
        public string DisplayName { get; set; } = null!;

        // The ingredient line as written ("Salt and pepper to taste", "1 cup butter,
        // divided") -- what the recipe shows. Quantity/Measurement below are parsed from
        // it and only drive shopping math. Null for ingredients saved before this
        // existed; callers fall back to formatting Quantity/Measurement/DisplayName.
        [MaxLength(500)]
        public string? OriginalText { get; set; }

        // 0 = no set amount ("to taste", "as needed").
        [Column(TypeName = "decimal(10,2)")]
        public decimal Quantity { get; set; }

        public int? MeasurementId { get; set; }
        public Measurement? Measurement { get; set; }

        // The chosen “buy this” product (optional)
        public string? SelectedKrogerUpc { get; set; }
        public KrogerProduct? SelectedKrogerProduct { get; set; }

        public string? Section { get; set; }
        public int SortOrder { get; set; }
    }
}