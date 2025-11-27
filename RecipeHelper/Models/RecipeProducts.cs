using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Permissions;

namespace RecipeHelper.Models
{
    public class RecipeProduct
    {
        [Key]
        public int Id { get; set; }
        public int RecipeId { get; set; }
        public int ProductId { get; set; }
        [Column(TypeName = "decimal(10,2)")]
        public decimal Quantity { get; set; }
        public int? MeasurementId { get; set; }
        public Measurement Measurement { get; set; }
        public Recipe Recipe { get; set; }
        public Product Product { get; set; }
    }
}