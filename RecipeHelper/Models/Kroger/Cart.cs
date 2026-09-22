using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RecipeHelper.Models.Kroger
{

    // Where an add-to-cart flow started -- carried through PreviewAddToCart's hidden
    // form field and the BeginAddToCart/CompleteAddToCart session round-trip (Cart.cs
    // "PendingCart" JSON) so CartController knows where to send the user back to on
    // success or failure. Defaults to MealPlan since that's the original, longest-
    // standing flow (Dinner/ReviewDinnerSelections and, since #165, a single recipe's
    // "Add to Cart" both post into it the same way); Products (#166) is the only other
    // flow that needs a different return destination today.
    public static class CartOrigin
    {
        public const string MealPlan = "MealPlan";
        public const string Products = "Products";
    }

    public class AddToCartVM
    {
        public List<CartItemVM> Items { get; set; } = new List<CartItemVM>();
        public string Origin { get; set; } = CartOrigin.MealPlan;

    }

    public class CartItemVM
    {
        public string Upc { get; set; } = null!;
        // The review page already posts Items[i].Name (ReviewDinnerSelections.cshtml) --
        // this property just lets it actually bind, so a skipped/unmapped ingredient can
        // be reported by name instead of vanishing with no identifying detail.
        public string? Name { get; set; }
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
                    // Round up, not truncate -- every upstream conversion branch already
                    // produces whole numbers via Math.Ceiling, but a straight (int) cast
                    // would silently under-order (2.7 -> 2) if that ever changes, e.g. a
                    // future editable-quantity input on the preview screen.
                    Quantity = (int)Math.Ceiling(item.Quantity)
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
        public string? ConversionNote { get; set; }
        public string? OriginalIngredient { get; set; }
        public string? KrogerPackSize { get; set; }
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
        public string? ConversionNote { get; set; }
        public string? OriginalIngredient { get; set; }
    }

    public class AddToCartPreviewVM
    {
        public List<AddToCartPreviewItemVM> Items { get; set; } = new();
        public List<SkippedCartItem> Skipped { get; set; } = new();
        public string Origin { get; set; } = CartOrigin.MealPlan;
    }

    // An ingredient ConvertIngredientsToCartItems couldn't turn into a cart line --
    // either it had no mapped Kroger product, or the product lookup failed. Surfaced
    // to the user instead of the item just disappearing between the review page and
    // the cart preview with no explanation.
    public class SkippedCartItem
    {
        public string Name { get; set; } = "";
        public string Reason { get; set; } = "";
        public decimal Quantity { get; set; }
    }

    public class ConvertIngredientsResult
    {
        public List<DetailedCartItem> Items { get; set; } = new();
        public List<SkippedCartItem> Skipped { get; set; } = new();
    }

}
