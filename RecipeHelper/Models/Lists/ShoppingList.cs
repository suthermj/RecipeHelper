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

        public List<ShoppingListItem> Items { get; set; } = new();
    }

    public class ShoppingListItem
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Quantity { get; set; } = 1;
        public bool IsCompleted { get; set; }

        // Kroger product link (optional)
        public string? Upc { get; set; }

        [ForeignKey("Upc")]
        public KrogerProduct? KrogerProduct { get; set; }

        // Parent list
        public int ShoppingListId { get; set; }
        public ShoppingList ShoppingList { get; set; } = null!;
    }
}
