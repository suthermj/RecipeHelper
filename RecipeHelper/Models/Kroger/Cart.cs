using System.Collections.Generic;

namespace RecipeHelper.Models.Kroger
{

    public class AddToCartVM
    {
        public List<CartItemVM> Items { get; set; } = new List<CartItemVM>();

    }

    public class CartItemVM
    {
        public string Upc { get; set; } = null!;
        public decimal Quantity { get; set; }
        public string Measurement { get; set; }
        public string Modality { get; set; } = "PICKUP";
        public bool Include { get; set; }

    }

    public class CartItem
    {
        public string Upc { get; set; } = null!;
        public int Quantity { get; set; }
        public string Modality { get; set; } = "PICKUP";

    }

    public class AddToCartRequest
    {
        public List<CartItem> Items { get; set; } = new List<CartItem>();
        public AddToCartRequest(List<CartItemVM> cartItems)
        {
            foreach (var item in cartItems)
            {
                Items.Add(new CartItem
                {
                    Upc = item.Upc,
                    Quantity = (int)item.Quantity
                });
            }
        }

        public AddToCartRequest()
        {
        }
    }

    public class CartOverview
    {

    }

    public class DetailedCartItem
    {
        public string Name { get; set; }
        public string Upc { get; set; }
        public string Aisle { get; set; }
        public float RegularPrice { get; set; }
        public float PromoPrice { get; set; }
        public string StockLevel { get; set; }
        public bool OnSale { get; set; }
        public int Quantity { get; set; }
        public string Brand { get; set; }
        public List<string> Categories { get; set; }
    }
}
