using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RecipeHelper.Models.Kroger;

namespace RecipeHelper.Models.Lists
{
    public class ShoppingList
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public string? StoreId { get; set; }  // Kroger location ID at time of creation

        public List<ShoppingListItem> Items { get; set; } = new();
    }

    public class ShoppingListItem
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public bool IsCompleted { get; set; }

        // Aisle/price data populated from Kroger API at list creation time
        public string? AisleNumber { get; set; }       // e.g. "100" — for sort ordering
        public string? AisleDescription { get; set; }  // e.g. "DAIRY" — for section grouping
        public string? Brand { get; set; }
        public decimal? Price { get; set; }
        public decimal? PromoPrice { get; set; }

        // Kroger product link (optional)
        public string? Upc { get; set; }

        [ForeignKey("Upc")]
        public KrogerProduct? KrogerProduct { get; set; }

        // Parent list
        public int ShoppingListId { get; set; }
        public ShoppingList ShoppingList { get; set; } = null!;
    }
}
