namespace RecipeHelper.Models.Kroger
{
    public class ProductResponse
    {
        public List<Product> products { get; set; } = [];
    }

    public class Product
    {
        public string ProductId { get; set; }
        public string Upc { get; set; }
        public List<string> Categories { get; set; }
        public string Description { get; set; }

        public string SoldBy { get; set; }
        public string Size { get; set; }
        public Price Price { get; set; }
    }

    public class Price {
        public decimal Regular { get; set; } 
        public decimal Promo { get; set; }
    }

}
