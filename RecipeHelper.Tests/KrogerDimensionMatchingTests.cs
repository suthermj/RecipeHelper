using RecipeHelper.Models.Kroger;
using RecipeHelper.Services;
using RecipeHelper.Utility;
using Xunit;

namespace RecipeHelper.Tests
{
    // Covers Stage 0 fix #2's downstream effect: KrogerService.AreSameDimension used to
    // treat PackDimension.Composite as matching BOTH volume and weight ingredients (since
    // every composite pack was labeled Composite regardless of its actual primary unit).
    // A cups-measured ingredient against "8 ct / 22 oz" (weight) was accepted as "same
    // dimension" and divided teaspoons by grams -- a meaningless ratio, silently returned
    // with no conversion note. Now that KrogerSizeParser reports the pack's real
    // dimension, these should no longer be considered directly compatible.
    public class KrogerDimensionMatchingTests
    {
        [Fact]
        public void AreSameDimension_VolumeIngredient_AgainstWeightPack_IsFalse()
        {
            // Before the fix this pack's Dimension would have been PackDimension.Composite,
            // which AreSameDimension(Volume, Composite) treated as true.
            Assert.False(KrogerService.AreSameDimension(MeasureDimension.Volume, PackDimension.Weight));
        }

        [Fact]
        public void AreSameDimension_WeightIngredient_AgainstVolumePack_IsFalse()
        {
            Assert.False(KrogerService.AreSameDimension(MeasureDimension.Weight, PackDimension.Volume));
        }

        [Fact]
        public void AreSameDimension_VolumeIngredient_AgainstVolumePack_IsTrue()
        {
            Assert.True(KrogerService.AreSameDimension(MeasureDimension.Volume, PackDimension.Volume));
        }

        [Fact]
        public void AreSameDimension_WeightIngredient_AgainstWeightPack_IsTrue()
        {
            Assert.True(KrogerService.AreSameDimension(MeasureDimension.Weight, PackDimension.Weight));
        }

        [Fact]
        public void IsCrossDimension_VolumeIngredient_AgainstWeightPack_IsTrue()
        {
            // This is the correct bucket for the "cups ingredient vs. 8ct/22oz pack" case
            // now that the pack's real Weight dimension is reported -- it routes into the
            // density-based cross-dimension conversion (with an explicit note) instead of
            // the same-dimension ratio that used to run on mismatched units.
            Assert.True(KrogerService.IsCrossDimension(MeasureDimension.Volume, PackDimension.Weight));
        }
    }
}
