using RecipeHelper.Models.Kroger;
using RecipeHelper.Utility;
using Xunit;

namespace RecipeHelper.Tests
{
    // Covers Stage 0 fix #1: KrogerSizeParser.TryParse never returns null -- it signals
    // failure via ParsedOk on the returned instance. These pin the parser's own contract
    // so a regression in KrogerPackInfo.BuildPackInfo (which used to ignore ParsedOk
    // entirely) would be caught here too.
    public class KrogerSizeParserTests
    {
        [Theory]
        [InlineData("Varies")]
        [InlineData("N/A")]
        [InlineData("About 1 lb")]
        [InlineData("1 lb 4 oz")]
        [InlineData("")]
        [InlineData(null)]
        public void TryParse_UnparseableSize_ReportsFailure(string? size)
        {
            var result = KrogerSizeParser.TryParse(size);

            Assert.False(result.ParsedOk);
        }

        [Fact]
        public void TryParse_SimpleWeightSize_Succeeds()
        {
            var result = KrogerSizeParser.TryParse("15 oz");

            Assert.True(result.ParsedOk);
            Assert.Equal(15m, result.PrimaryQty);
            Assert.Equal(PackDimension.Weight, result.Dimension);
        }

        // Stage 0 fix #2: a composite pack's Dimension must reflect its real primary
        // measurement (Weight/Volume/Unit), not a blanket PackDimension.Composite that
        // used to make it look dimensionally compatible with both weight and volume
        // ingredients regardless of what it actually contains.
        [Fact]
        public void TryParse_CompositeWeightPack_DimensionIsWeightNotComposite()
        {
            var result = KrogerSizeParser.TryParse("8 ct / 22 oz");

            Assert.True(result.ParsedOk);
            Assert.True(result.IsComposite);
            Assert.Equal(8m, result.CountEach);
            Assert.Equal(22m, result.PrimaryQty);
            Assert.Equal(PackDimension.Weight, result.Dimension);
            Assert.NotEqual(PackDimension.Composite, result.Dimension);
        }

        [Fact]
        public void TryParse_CompositeVolumePack_DimensionIsVolume()
        {
            var result = KrogerSizeParser.TryParse("12 ct / 16.9 fl oz");

            Assert.True(result.ParsedOk);
            Assert.Equal(PackDimension.Volume, result.Dimension);
        }
    }
}
