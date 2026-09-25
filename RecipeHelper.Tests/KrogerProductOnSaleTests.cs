using RecipeHelper.Models.Kroger;
using RecipeHelper.Utility;
using Xunit;

namespace RecipeHelper.Tests
{
    // ViewProduct shows the promo price (and struck-through regular price) only when
    // KrogerProductDto.onSale is true. It used to be a plain settable property that
    // ToKrogerProduct never assigned, so every product read as "not on sale" and the
    // product page always showed the regular price, even when Kroger returned a promo.
    public class KrogerProductOnSaleTests
    {
        private static KrogerProductModel ButterWithPrice(float regular, float promo) => new KrogerProductModel
        {
            upc = "0001111089307",
            description = "Kroger® Salted Butter Sticks",
            items = new[] { new Item { price = new Price { regular = regular, promo = promo }, size = "1 lb", soldBy = "UNIT" } },
        };

        [Fact]
        public void PromoBelowRegular_IsOnSale()
        {
            var dto = ButterWithPrice(regular: 4.99f, promo: 3.99f).ToKrogerProduct();

            Assert.True(dto.onSale);
            Assert.Equal(3.99f, dto.promoPrice);
            Assert.Equal(4.99f, dto.regularPrice);
        }

        [Fact]
        public void NoPromo_IsNotOnSale()
        {
            Assert.False(ButterWithPrice(regular: 4.99f, promo: 0f).ToKrogerProduct().onSale);
        }

        // Kroger sometimes returns promo == regular when there's no actual discount.
        [Fact]
        public void PromoEqualToRegular_IsNotOnSale()
        {
            Assert.False(ButterWithPrice(regular: 4.99f, promo: 4.99f).ToKrogerProduct().onSale);
        }

        [Fact]
        public void OnSale_FlowsThroughToDetailedCartItem()
        {
            var item = ButterWithPrice(regular: 4.99f, promo: 3.99f).ToKrogerProduct().ToDetailedCartItem(1);

            Assert.True(item.OnSale);
        }
    }
}
