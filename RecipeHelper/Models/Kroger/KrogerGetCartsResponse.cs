namespace RecipeHelper.Models.Kroger.Carts
{

    public class KrogerGetCartsResponse
    {
        public Datum[] data { get; set; }
    }

    public class Datum
    {
        public string id { get; set; }
        public string name { get; set; }
        public Item[] items { get; set; }
        public DateTime createdDate { get; set; }
    }

    public class Item
    {
        public bool allowSubstitutes { get; set; }
        public DateTime createdDate { get; set; }
        public int quantity { get; set; }
        public string specialInstructions { get; set; }
        public string upc { get; set; }
        public string description { get; set; }
        public string modality { get; set; }
    }

}