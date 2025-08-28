using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace RecipeHelper.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Upc { get; set; }
        public decimal Price { get; set; }

        // Navigations
        public List<RecipeProduct>? RecipeProducts { get; set; }
    }

    public class ProductVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Upc { get; set; }
        public int Quantity { get; set; }
        public int? MeasurementId { get; set; }

    }

    public class ViewProductVM
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Upc { get; set; }

    }

    public class IngredientsVM
    {
        public int RecipeId { get; set; }
        public string RecipeName { get; set; }
        public string ImageUri { get; set; }
        public List<ProductVM> Ingredients { get; set; }
        public IEnumerable<SelectListItem> AvailableMeasurements { get; set; }

    }
}