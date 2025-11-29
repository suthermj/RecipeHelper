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


        public static string? GetSmallestMeasurementUnit(string? unitType)
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
                    result = quantity;
                    break;
                case "tablespoons":
                    result = quantity * 3; // 1 tablespoon = 3 teaspoons
                    break;
                case "cups":
                    result = quantity * 48; // 1 cup = 48 teaspoons
                    break;
                case "pints":
                    result = quantity * 96; // 1 cup = 48 teaspoons
                    break;
                case "ounces":
                    result = quantity;
                    break;
                case "pounds":
                    result = quantity * 16; // 1 pound = 16 ounces
                    break;
                case "unit":
                    result = quantity;
                    break;
                default:
                    throw new ArgumentException($"Unsupported measurement unit [{fromUnit}]");
            }
            return result;
        }

        public static string NormalizeMeasurementUnit(string measurementName)
        {
            if (string.IsNullOrWhiteSpace(measurementName)) return null;
            // normalize common abbreviations here if you want:
            // e.g. if (unit.Equals("tsp", StringComparison.OrdinalIgnoreCase)) unit = "Teaspoon";

            measurementName = measurementName.Trim();
            string normalizedMeasurementName = "";
            // 🔥 Normalize abbreviations → match DB naming
            switch (measurementName.ToLower())
            {
                case "tsp":
                case "tsps":
                case "teaspoon":
                case "teaspoons":
                    normalizedMeasurementName = "Teaspoons";
                    break;

                case "tbsp":
                case "tbsps":
                case "tablespoon":
                case "tablespoons":
                    normalizedMeasurementName = "Tablespoons";
                    break;

                case "cup":
                case "cups":
                    normalizedMeasurementName = "Cups";
                    break;

                case "pint":
                case "pt":
                case "pints":
                    normalizedMeasurementName = "Pints";
                    break;

                case "oz":
                case "ounce":
                case "ounces":
                    normalizedMeasurementName = "Ounces";
                    break;

                case "lb":
                case "lbs":
                case "pound":
                case "pounds":
                    normalizedMeasurementName = "Pounds";
                    break;

                case "unit":
                case "units":
                case "piece":
                case "pieces":
                    normalizedMeasurementName = "Unit";
                    break;

                    // Add more as needed
            }

            return normalizedMeasurementName;
        }
    }
}
