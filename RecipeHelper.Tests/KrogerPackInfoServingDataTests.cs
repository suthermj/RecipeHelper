using RecipeHelper.Models.Kroger;
using RecipeHelper.Utility;
using Xunit;

namespace RecipeHelper.Tests
{
    // Kroger's product-details API returns nutrition-panel serving data
    // (nutritionInformation[0].servingSize + servingsPerPackage) that's exact,
    // manufacturer-reported total pack content -- e.g. the real "Spice World Minced
    // Garlic 8 oz" (UPC 0007096900009) returns servingSize = 1 tsp, servingsPerPackage =
    // 45, i.e. the jar holds 45 tsp. That data existed in the raw API response the whole
    // time; only servingSize.unitOfMeasure.name was ever captured (into an otherwise
    // unused field), and never the quantity or serving count. KrogerPackInfo now prefers
    // this over parsing the free-text size string, which is ambiguous (is "32 oz" fluid
    // or weight ounces?) and, for a dimension mismatch, forced a density guess.
    public class KrogerPackInfoServingDataTests
    {
        // Real response shape for UPC 0007096900009, fetched directly against the
        // Kroger API's /products/{upc} endpoint.
        private static KrogerProductDto GarlicJarWithServingData() => new KrogerProductDto
        {
            upc = "0007096900009",
            name = "Spice World Minced Garlic 8 oz",
            brand = "Spice World",
            size = "8 oz",
            soldBy = "UNIT",
            categories = new List<string> { "Baking Goods" },
            stockLevel = "HIGH",
            servingSizeQty = 1,
            servingSizeUnitAbbreviation = "tsp",
            servingsPerPackage = 45,
        };

        // Real response shape for UPC 0001111004969 ("Kroger® 99% Fat Free Chicken
        // Broth"): item size is "32 oz" (ambiguous), but servingSize is 240 ml x 4
        // servings = 960 ml total.
        private static KrogerProductDto BrothCartonWithServingData() => new KrogerProductDto
        {
            upc = "0001111004969",
            name = "Kroger® 99% Fat Free Chicken Broth",
            brand = "Kroger",
            size = "32 oz",
            soldBy = "UNIT",
            categories = new List<string> { "Soup" },
            stockLevel = "HIGH",
            servingSizeQty = 240,
            servingSizeUnitAbbreviation = "ml",
            servingsPerPackage = 4,
        };

        [Fact]
        public void ServingData_Present_TakesPrecedenceOverSizeString()
        {
            var pack = KrogerPackInfo.BuildPackInfo(GarlicJarWithServingData());

            Assert.True(pack.ParsedOk);
            Assert.True(pack.FromServingData);
            Assert.Equal(45m, pack.PrimaryQty); // 1 tsp x 45 servings
            Assert.Equal("tsp", pack.PrimaryUnit);
            Assert.Equal(PackDimension.Volume, pack.Dimension);
        }

        [Fact]
        public void ServingData_BrothInMilliliters_ParsesToVolumeDimension()
        {
            var pack = KrogerPackInfo.BuildPackInfo(BrothCartonWithServingData());

            Assert.True(pack.FromServingData);
            Assert.Equal(960m, pack.PrimaryQty); // 240 ml x 4 servings
            Assert.Equal(PackDimension.Volume, pack.Dimension);
        }

        [Fact]
        public void NoServingData_FallsBackToSizeStringParsing()
        {
            var product = new KrogerProductDto
            {
                upc = "0009999012345", name = "Plain Product", size = "15 oz", soldBy = "UNIT",
                categories = new List<string>(), stockLevel = "HIGH",
            };

            var pack = KrogerPackInfo.BuildPackInfo(product);

            Assert.False(pack.FromServingData);
            Assert.True(pack.ParsedOk);
            Assert.Equal(15m, pack.PrimaryQty);
            Assert.Equal(PackDimension.Weight, pack.Dimension);
        }

        [Fact]
        public void ServingData_WithUnrecognizedUnit_FallsBackToSizeStringParsing()
        {
            var product = new KrogerProductDto
            {
                upc = "0009999054321", name = "Odd Unit Product", size = "10 oz", soldBy = "UNIT",
                categories = new List<string>(), stockLevel = "HIGH",
                servingSizeQty = 2, servingSizeUnitAbbreviation = "xyz", servingsPerPackage = 5,
            };

            var pack = KrogerPackInfo.BuildPackInfo(product);

            Assert.False(pack.FromServingData);
            Assert.True(pack.ParsedOk);
            Assert.Equal(10m, pack.PrimaryQty);
        }
    }
}
