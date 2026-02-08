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

        [Column(TypeName = "decimal(10,2)")]
        public decimal Quantity { get; set; }

        public int? MeasurementId { get; set; }
        public Measurement? Measurement { get; set; }

        // The chosen “buy this” product (optional)
        public string? SelectedKrogerUpc { get; set; }
        public KrogerProduct? SelectedKrogerProduct { get; set; }
    }
}