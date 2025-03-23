using System.ComponentModel.DataAnnotations;
using System.Security.Permissions;

namespace RecipeHelper.Models
{
    public class RecipeProduct
    {
        [Key]
        public int Id { get; set; }
        public int RecipeId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }

        public Recipe Recipe { get; set; }
        public Product Product { get; set; }
    }
}