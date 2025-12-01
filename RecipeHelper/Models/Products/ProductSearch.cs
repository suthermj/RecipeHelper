using RecipeHelper.Models.Kroger;

namespace RecipeHelper.Models.Products
{
    public class ProductSearchVM
    {
        public string SearchTerm { get; set; }
        public List<Kroger.KrogerProduct>? ProductSearchResults { get; set; }
    }
}
