namespace RecipeHelper.Models.Kroger
{
    public class Product
    {
        public string ProductId { get; set; }
        public string upc { get; set; }
        public List<string> categories { get; set; }
        public string description { get; set; }
        public string aisleLocation { get; set; }
        public string brand { get; set; }

        public string soldBy { get; set; }
        public string size { get; set; }
        public decimal sizeQuantity    
        {
            get                             // Computed getter
            {
                return Convert.ToDecimal(size.Split(" ")[0]);
            }
        }
        public string sizeUnit
        {
            get                             // Computed getter
            {
                return size.Split(" ")[1];
            }
        }
        public float regularPrice { get; set; }
        public float promoPrice { get; set; }
        public string stockLevel { get; set; }
        public bool onSale { get; set; }

        public bool HasMissingData()
        {
            return size == "N/A" ||
                   soldBy == "N/A" ||
                   stockLevel == "N/A" ||
                   string.IsNullOrWhiteSpace(description) ||
                   promoPrice == 0;
        }
    }

    public class KrogerProductSearchResponse
    {
        public KrogerProduct[] data { get; set; }
        public Meta meta { get; set; }
    }

    public class KrogerProductDetailsResponse
    {
        public KrogerProduct data { get; set; }
        public Meta meta { get; set; }
    }

    public class Meta
    {
        public Pagination pagination { get; set; }
    }

    public class Pagination
    {
        public int start { get; set; }
        public int limit { get; set; }
        public int total { get; set; }
    }

    public class KrogerProduct
    {
        public string productId { get; set; }
        public string upc { get; set; }
        public Aislelocation[] aisleLocations { get; set; }
        public string brand { get; set; }
        public string[] categories { get; set; }
        public string countryOrigin { get; set; }
        public string description { get; set; }
        public Image[] images { get; set; }
        public Item[] items { get; set; }
    }

    public class Aislelocation
    {
        public string bayNumber { get; set; }
        public string description { get; set; }
        public string number { get; set; }
        public string numberOfFacings { get; set; }
        public string side { get; set; }
        public string shelfNumber { get; set; }
        public string shelfPositionInBay { get; set; }
    }

    public class Image
    {
        public string perspective { get; set; }
        public Size[] sizes { get; set; }
        public bool featured { get; set; }
    }

    public class Size
    {
        public string size { get; set; }
        public string url { get; set; }
    }

    public class Item
    {
        public string itemId { get; set; }
        public Inventory inventory { get; set; }
        public bool favorite { get; set; }
        public Fulfillment fulfillment { get; set; }
        public Price price { get; set; }
        public string size { get; set; }
        public string soldBy { get; set; }
    }

    public class Inventory
    {
        public string stockLevel { get; set; }
    }

    public class Fulfillment
    {
        public bool curbside { get; set; }
        public bool delivery { get; set; }
        public bool inStore { get; set; }
        public bool shipToHome { get; set; }
    }

    public class Price
    {
        public float regular { get; set; }
        public float promo { get; set; }
    }

}
