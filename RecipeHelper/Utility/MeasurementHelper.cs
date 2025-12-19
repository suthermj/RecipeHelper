namespace RecipeHelper.Utility
{
    public static class MeasurementHelper
    {
        public static string GetMeasurementUnitType(string unit)
        {
            if (string.IsNullOrWhiteSpace(unit)) return null;
            // normalize common abbreviations here if you want:
            // e.g. if (unit.Equals("tsp", StringComparison.OrdinalIgnoreCase)) unit = "Teaspoon";

            unit = unit.Trim();
            string unitType = null;

            // 🔥 Normalize abbreviations → match DB naming
            switch (unit.ToLower())
            {
                case "millilitre":
                case "millilitres":
                case "tsp":
                case "tsps":
                case "teaspoon":
                case "teaspoons":
                case "tbsp":
                case "tbsps":
                case "tablespoon":
                case "tablespoons":
                case "cup":
                case "cups":
                case "pt":
                case "pint":
                    unitType = "Volume";
                    break;

                case "oz":
                case "gram":
                case "grams":
                case "ounce":
                case "ounces":
                case "lb":
                case "lbs":
                case "pound":
                case "pounds":
                    unitType = "Weight";
                    break;

                case "unit":
                case "units":
                case "piece":
                case "pieces":
                    unitType = "Unit";
                    break;

                    // Add more as needed
            }

            return unitType;
        }


        /*public static string? GetSmallestMeasurementUnit(string? unitType)
        {
            if (string.IsNullOrWhiteSpace(unitType)) return null;
            // normalize common abbreviations here if you want:
            // e.g. if (unit.Equals("tsp", StringComparison.OrdinalIgnoreCase)) unit = "Teaspoon";

            unitType = unitType.Trim();
            string smallestUnit = null;

            // 🔥 Normalize abbreviations → match DB naming
            switch (unitType.ToLower())
            {
                case "Volume":
                    smallestUnit = "Teaspoon";
                    break;

                case "Weight":
                    smallestUnit = "Ounce";
                    break;

                case "unit":
                case "units":
                case "piece":
                case "pieces":
                    smallestUnit = "Unit";
                    break;

                    // Add more as needed
            }

            return smallestUnit;
        }

        public static decimal ConvertToLowestMeasurementUnit(string fromUnit, decimal quantity)
        {
            decimal result = 0;
            switch (fromUnit.ToLower())
            {
                case "teaspoons":
                    return quantity;
                case "tablespoons":
                    return quantity * 3; // 1 tablespoon = 3 teaspoons
                case "cups":
                    return quantity * 48; // 1 cup = 48 teaspoons
                case "pints":
                    return quantity * 96; // 1 cup = 48 teaspoons
                case "ounces":
                    return quantity;
                case "pounds":
                    return quantity * 16; // 1 pound = 16 ounces
                case "unit":
                    return quantity;
                default:
                    throw new ArgumentException($"Unsupported measurement unit [{fromUnit}]");
            }
            return result;
        }
        */
        public static decimal ConvertVolumeToBaseUnit(string fromUnit, decimal quantity)
        {
            if (quantity < 0)
                throw new ArgumentException("Quantity cannot be negative.");

            if (string.IsNullOrWhiteSpace(fromUnit))
                throw new ArgumentException("Unit cannot be empty.");

            switch (fromUnit.ToLower())
            {
                case "teaspoons":
                    return quantity;
                case "tablespoons":
                    return quantity * 3; // 1 tablespoon = 3 teaspoons
                case "cups":
                    return quantity * 48; // 1 cup = 48 teaspoons
                case "pints":
                    return quantity * 96; // 1 cup = 48 teaspoons
                case "ounces":
                    return quantity * 6;
                default:
                    throw new ArgumentException($"Unsupported measurement unit [{fromUnit}]");
                    // Add more as needed
            }
        }

        public static decimal ConvertWeightToBaseUnit(string fromUnit, decimal quantity)
        {
            if (quantity < 0)
                throw new ArgumentException("Quantity cannot be negative.");

            if (string.IsNullOrWhiteSpace(fromUnit))
                throw new ArgumentException("Unit cannot be empty.");

            switch (fromUnit.ToLowerInvariant())
            {
                case "grams":
                    return quantity;

                case "milligrams":
                    return quantity / 1000m; // 1000 mg = 1 g

                case "ounces":
                    return quantity * 28.3495m; // 1 oz = 28.3495 g

                case "pounds":
                    return quantity * 453.592m; // 1 lb = 453.592 g

                case "kilograms":
                    return quantity * 1000m;

                default:
                    throw new ArgumentException($"Unsupported weight unit '{fromUnit}'.");
            }
        }

        public static string? NormalizeMeasurementUnit(string measurementName)
        {
            if (string.IsNullOrWhiteSpace(measurementName)) return "Unit";
            // normalize common abbreviations here if you want:
            // e.g. if (unit.Equals("tsp", StringComparison.OrdinalIgnoreCase)) unit = "Teaspoon";

            measurementName = measurementName.Trim();
            // 🔥 Normalize abbreviations → match DB naming
            switch (measurementName.ToLower())
            {
                case "tsp":
                case "tsps":
                case "teaspoon":
                case "teaspoons":
                    return "Teaspoons";

                case "tbsp":
                case "tbsps":
                case "tablespoon":
                case "tablespoons":
                    return "Tablespoons";

                case "cup":
                case "cups":
                    return  "Cups";

                case "pint":
                case "pt":
                case "pints":
                    return "Pints";

                case "oz":
                case "ounce":
                case "ounces":
                    return "Ounces";

                case "lb":
                case "lbs":
                case "pound":
                case "pounds":
                    return "Pounds";

                case "gram":
                case "grams":
                    return "Grams";

                case "unit":
                case "units":
                case "piece":
                case "pieces":
                case "count":
                    return "Unit";

                default:
                    return "Unit";

                    // Add more as needed
            }
        }
    }
}
