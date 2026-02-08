namespace RecipeHelper.Utility
{
    public static class MeasurementHelper
    {
        /// <summary>
        /// Normalizes a raw measurement string to the DB Measurement.Name format.
        /// Delegates to UnitConverter for parsing.
        /// </summary>
        public static string? NormalizeMeasurementUnit(string measurementName)
        {
            if (string.IsNullOrWhiteSpace(measurementName)) return "Unit";

            var parsed = UnitConverter.Parse(measurementName);
            return UnitConverter.ToDisplayName(parsed);
        }
    }
}
