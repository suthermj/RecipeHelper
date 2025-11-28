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
        public string Modality { get; set; } = "DELIVERY";

    }

    public class CartItem
    {
        public string Upc { get; set; } = null!;
        public int Quantity { get; set; }
        public string Modality { get; set; } = "DELIVERY";

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
    }
}
