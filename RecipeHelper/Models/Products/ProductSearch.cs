namespace RecipeHelper.Models.Products
{
    public class ProductSearchVM
    {
        public string SearchTerm { get; set; }
        public List<Kroger.KrogerProductDto>? ProductSearchResults { get; set; }
    }
}
