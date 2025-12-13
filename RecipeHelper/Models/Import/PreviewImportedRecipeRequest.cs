using System.ComponentModel.DataAnnotations;

namespace RecipeHelper.Models.Import
{
    public class PreviewImportedRecipeRequest
    {
        public string Title { get; set; } = "";
        public string? Image { get; set; }

        public List<PreviewImportedRecipeIngredient> Ingredients { get; set; } = new();
    }

    public class PreviewImportedRecipeIngredient
    {
        public string Name { get; set; } = "";  
        public decimal Amount { get; set; }      
        public string? Unit { get; set; }       
        public int? ProductId { get; set; }     
    }


    public class ImportPreview()
    {
        [Required]
        public string Title { get; set; } = "";

        public string? Image { get; set; }

        // Each imported ingredient with suggestions + Kroger fallback
        [MinLength(1)]
        public List<ImportPreviewIngredient> Ingredients { get; set; } = new();
    }

    public class ImportPreviewIngredient()
    {
        // Source (from Spoonacular parsing)
        [Required]
        public string Name { get; set; } = "";

        public decimal Amount { get; set; }  
        public string? Unit { get; set; }   

        public int? SuggestedProductId { get; set; }
        public string SuggestedProductUpc { get; set; }
        public string? SuggestedProductName { get; set; }

        public string? SuggestionKind { get; set; }

        public SuggestedKrogerProductPreview? Kroger { get; set; }
    }

    public class SuggestedKrogerProductPreview
    {
        [Required]
        public string Upc { get; set; } = "";

        public string? Name { get; set; }
        public string? ImageUrl { get; set; }

        public bool OnSale { get; set; }
        public decimal? RegularPrice { get; set; }
        public decimal? PromoPrice { get; set; }
    }
}
