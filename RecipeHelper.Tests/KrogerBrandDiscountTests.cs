using RecipeHelper.Utility;
using Xunit;

namespace RecipeHelper.Tests
{
    public class KrogerBrandDiscountTests
    {
        [Theory]
        [InlineData("Kroger", true)]
        [InlineData("Simple Truth Organic", true)]
        [InlineData("Private Selection", true)]
        [InlineData("HemisFares", true)]
        [InlineData("Land O Lakes", false)]
        [InlineData(null, false)]
        public void IsKrogerBrand(string? brand, bool expected) =>
            Assert.Equal(expected, KrogerBrandDiscount.IsKrogerBrand(brand));

        [Fact]
        public void KrogerBrand_RegularPrice_Takes10Percent() =>
            Assert.Equal(4.49m, KrogerBrandDiscount.EstimatedPrice("Kroger", 4.99m, 0m));

        // Stacks on the sale price, matching PreviewAddToCart's UnitPrice.
        [Fact]
        public void KrogerBrand_OnSale_Takes10PercentOffSalePrice() =>
            Assert.Equal(3.59m, KrogerBrandDiscount.EstimatedPrice("Kroger", 4.99m, 3.99m));

        [Fact]
        public void OtherBrand_OnSale_IsJustSalePrice() =>
            Assert.Equal(3.99m, KrogerBrandDiscount.EstimatedPrice("Land O Lakes", 4.99m, 3.99m));

        [Fact]
        public void OtherBrand_NoSale_IsRegularPrice() =>
            Assert.Equal(4.99m, KrogerBrandDiscount.EstimatedPrice("Land O Lakes", 4.99m, 0m));
    }
}
