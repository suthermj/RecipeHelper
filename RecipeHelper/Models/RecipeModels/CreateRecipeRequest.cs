using System.ComponentModel.DataAnnotations;

namespace RecipeHelper.Models.RecipeModels
{
    public class CreateRecipeRequest
    {
        public string Title { get; set; } = "";

        public IFormFile? ImageFile { get; set; }
        public List<CreateRecipeIngredientDto> Ingredients { get; set; } = new();
        public List<string> Instructions { get; set; } = new();
    }

    public class CreateRecipeIngredientDto
    {
        public string DisplayName { get; set; } = "";

        public decimal Quantity { get; set; }

        public int? MeasurementId { get; set; }

        public string? SelectedKrogerUpc { get; set; }
    }
}
