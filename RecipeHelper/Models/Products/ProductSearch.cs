using RecipeHelper.Models.Kroger;

namespace RecipeHelper.Models.Products
{
    public class ProductSearchVM
    {
        public string SearchTerm { get; set; }
        public List<Kroger.Product>? ProductSearchResults { get; set; }
    }
}
