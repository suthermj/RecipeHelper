using RecipeHelper.Models.Kroger;
using RecipeHelper.Utility;
using Xunit;

namespace RecipeHelper.Tests
{
    // Covers the mapping bug found live: MappingExtensions.ToKrogerProduct only read
    // unitOfMeasure.abbreviation, but Kroger doesn't always populate it -- the real
    // response for UPC 0007639700207 ("La Costena® Green Pickled Sliced Jalapeno
    // Peppers") returns servingSize.unitOfMeasure as {"code":"G21","name":"Cup US"}
    // with no "abbreviation" key at all, silently losing usable serving data.
    public class ToKrogerProductServingDataMappingTests
    {
        private static KrogerProductModel JalapenoModel() => new KrogerProductModel
        {
            productId = "0007639700207",
            upc = "0007639700207",
            description = "La Costena® Green Pickled Sliced Jalapeno Peppers",
            brand = "La Costena",
            categories = new[] { "Condiments" },
            aisleLocations = new[] { new Aislelocation { number = "12", description = "CONDIMENTS" } },
            items = new[] { new Item { size = "7 oz", soldBy = "UNIT", price = new Price { regular = 1.49f }, inventory = new Inventory { stockLevel = "HIGH" } } },
            nutritionInformation = new[]
            {
                new NutritionInformation
                {
                    servingSize = new ServingSize
                    {
                        quantity = 0.25m,
                        // Real shape: no `abbreviation` key present at all for this unit code.
                        unitOfMeasure = new UnitOfMeasure { code = "G21", name = "Cup US" },
                    },
                    servingsPerPackage = new ServingsPerPackage { value = 3, description = "3.0 About" },
                },
            },
        };

        [Fact]
        public void ToKrogerProduct_UnitOfMeasureWithoutAbbreviation_FallsBackToName()
        {
            var dto = JalapenoModel().ToKrogerProduct();

            Assert.Equal(0.25m, dto.servingSizeQty);
            Assert.Equal("Cup US", dto.servingSizeUnitAbbreviation);
            Assert.Equal(3m, dto.servingsPerPackage);
        }

        [Fact]
        public void ToKrogerProduct_ThenBuildPackInfo_UsesServingDataEndToEnd()
        {
            var dto = JalapenoModel().ToKrogerProduct();
            var pack = KrogerPackInfo.BuildPackInfo(dto);

            Assert.True(pack.FromServingData);
            Assert.Equal(0.75m, pack.PrimaryQty); // 0.25 Cup US x 3 servings
            Assert.Equal(PackDimension.Volume, pack.Dimension);
        }
    }
}
