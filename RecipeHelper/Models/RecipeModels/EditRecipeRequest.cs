using System.ComponentModel.DataAnnotations;

namespace RecipeHelper.Models.RecipeModels
{
    public class EditRecipeRequest
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";

        public IFormFile? ImageFile { get; set; }
        public List<EditRecipeIngredientDto> Ingredients { get; set; } = new();
    }

    public class EditRecipeIngredientDto
    {
        public int Id { get; set; }                 // RecipeIngredient PK (0 = new row)

        public string DisplayName { get; set; } = "";

        public decimal Quantity { get; set; }

        public int? MeasurementId { get; set; }

        public int IngredientId { get; set; }

        public string? SelectedKrogerUpc { get; set; }
    }
}
