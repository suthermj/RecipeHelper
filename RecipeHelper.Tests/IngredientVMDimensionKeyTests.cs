using RecipeHelper.Models;
using Xunit;

namespace RecipeHelper.Tests
{
    // Covers the key scheme #78 (group ingredients by recipe on the review page) relies
    // on to keep the section view and the by-recipe view in sync. DinnerController.
    // SubmitDinnerSelections can emit up to 3 merged rows for one ingredient name when
    // entries span dimensions (volume/weight/unit -- DinnerController.cs:287-326), and
    // the "best display unit" it picks for a merged row (PickBestVolumeDisplay etc.)
    // need not match any single recipe's original measurement string. DimensionKey must
    // bucket by dimension family, not by the literal Measurement string, so a recipe-view
    // row and its corresponding merged section-view row always share a key.
    public class IngredientVMDimensionKeyTests
    {
        [Theory]
        [InlineData("Cups", "Tablespoons")]
        [InlineData("Cups", "Teaspoons")]
        [InlineData("Grams", "Ounces")]
        [InlineData("Grams", "Pounds")]
        public void SameNameSameDimensionFamily_DifferentExactUnit_ProducesSameKey(string measurementA, string measurementB)
        {
            var a = new IngredientVM { Name = "flour", Measurement = measurementA };
            var b = new IngredientVM { Name = "flour", Measurement = measurementB };

            Assert.Equal(a.DimensionKey, b.DimensionKey);
        }

        [Fact]
        public void SameName_DifferentDimensionFamily_ProducesDifferentKeys()
        {
            var volume = new IngredientVM { Name = "garlic", Measurement = "Teaspoons" };
            var unit = new IngredientVM { Name = "garlic", Measurement = "Unit" };

            Assert.NotEqual(volume.DimensionKey, unit.DimensionKey);
        }

        [Fact]
        public void NameIsTrimmedAndLowercased()
        {
            var a = new IngredientVM { Name = "  Garlic  ", Measurement = "Teaspoons" };
            var b = new IngredientVM { Name = "garlic", Measurement = "Tablespoons" };

            Assert.Equal(a.DimensionKey, b.DimensionKey);
        }

        [Fact]
        public void UnrecognizedMeasurement_BucketsAsUnit_SameAsCount()
        {
            // Matches DinnerController's aggregation switch, whose `default:` case lumps
            // Count and Unknown-dimension entries into the same "Unit" bucket.
            var count = new IngredientVM { Name = "salsa", Measurement = "Unit" };
            var unrecognized = new IngredientVM { Name = "salsa", Measurement = "SomeWeirdUnit" };

            Assert.Equal(count.DimensionKey, unrecognized.DimensionKey);
        }

        [Fact]
        public void DifferentNames_SameDimension_ProduceDifferentKeys()
        {
            var flour = new IngredientVM { Name = "flour", Measurement = "Cups" };
            var sugar = new IngredientVM { Name = "sugar", Measurement = "Cups" };

            Assert.NotEqual(flour.DimensionKey, sugar.DimensionKey);
        }
    }
}
