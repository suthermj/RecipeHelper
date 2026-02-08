using System.ComponentModel.DataAnnotations;
using RecipeHelper.Models.IngredientModels;

namespace RecipeHelper.Models.Kroger
{
    public class KrogerProduct
    {
        // Use UPC as PK (string)
        [Key]
        public string Upc { get; set; } = null!;

        public string Name { get; set; } = null!;
        public decimal Price { get; set; }

        public List<IngredientKrogerProduct> IngredientMappings { get; set; } = new();
        public List<RecipeIngredient> RecipeIngredients { get; set; } = new();
    }

    public class KrogerDatabaseProduct
    {
        public string Upc { get; set; } = null!;

        public string Name { get; set; } = null!;
    }
}
