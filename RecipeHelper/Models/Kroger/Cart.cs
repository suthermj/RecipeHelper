using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

    public class AddToCartPreviewItemVM
    {
        public string Upc { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Brand { get; set; } = "";
        public string Size { get; set; } = "";         // e.g. "32 oz"
        public string Aisle { get; set; } = "";
        public int QuantityToAdd { get; set; }         // how many units will be added to Kroger cart
        public float RegularPrice { get; set; }        // optional
        public float PromoPrice { get; set; }          // optional
        public bool OnSale  {
            get {
                return PromoPrice != 0 && PromoPrice < RegularPrice;
                    }
        }
        public bool Include { get; set; } = true;      // checkbox in UI
        public string StockLevel { get; set; }
    }

    public class AddToCartPreviewVM
    {
        public List<AddToCartPreviewItemVM> Items { get; set; } = new();
    }

}
