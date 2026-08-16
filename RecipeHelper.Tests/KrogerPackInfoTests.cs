using RecipeHelper.Models.Kroger;
using Xunit;

namespace RecipeHelper.Tests
{
    // Covers Stage 0 fix #1 at the actual bug site: KrogerPackInfo.BuildPackInfo used to
    // check `if (parsed != null)` -- always true, since KrogerSizeParser.TryParse never
    // returns null -- and hardcode ParsedOk = true, discarding parsed.ParsedOk entirely.
    // That silently disabled the "could not parse product size" fallback in
    // KrogerService.ConvertIngredientsToCartItems for every unparseable size string.
    public class KrogerPackInfoTests
    {
        private static KrogerProductDto MakeProduct(string upc, string size, string soldBy = "UNIT") =>
            new KrogerProductDto { upc = upc, name = "Test Product", size = size, soldBy = soldBy, categories = new List<string>() };

        [Theory]
        [InlineData("Varies")]
        [InlineData("N/A")]
        [InlineData("1 lb 4 oz")]
        public void BuildPackInfo_UnparseableSize_ParsedOkIsFalse(string size)
        {
            var pack = KrogerPackInfo.BuildPackInfo(MakeProduct("111", size));

            Assert.False(pack.ParsedOk);
        }

        [Fact]
        public void BuildPackInfo_ParseableSize_ParsedOkIsTrue()
        {
            var pack = KrogerPackInfo.BuildPackInfo(MakeProduct("222", "15 oz"));

            Assert.True(pack.ParsedOk);
            Assert.Equal(PackDimension.Weight, pack.Dimension);
        }

        [Fact]
        public void BuildPackInfo_CompositePack_DimensionReflectsPrimaryUnit()
        {
            var pack = KrogerPackInfo.BuildPackInfo(MakeProduct("333", "8 ct / 22 oz"));

            Assert.True(pack.ParsedOk);
            Assert.True(pack.IsComposite);
            Assert.Equal(PackDimension.Weight, pack.Dimension);
        }
    }
}
