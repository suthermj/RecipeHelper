namespace RecipeHelper.Utility
{
    /// <summary>
    /// Approximate densities (grams per milliliter) for common cooking ingredients.
    /// Used to bridge volume ↔ weight conversions when the ingredient and
    /// Kroger product are measured in different dimensions.
    /// </summary>
    public static class DensityTable
    {
        // 1 teaspoon = 4.92892 ml
        private const decimal MlPerTeaspoon = 4.92892m;

        private static readonly Dictionary<string, decimal> Densities =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Flours
                ["flour"]               = 0.53m,
                ["all-purpose flour"]   = 0.53m,
                ["all purpose flour"]   = 0.53m,
                ["bread flour"]         = 0.55m,
                ["cake flour"]          = 0.48m,
                ["whole wheat flour"]   = 0.51m,

                // Sugars
                ["sugar"]               = 0.85m,
                ["granulated sugar"]    = 0.85m,
                ["brown sugar"]         = 0.83m,
                ["powdered sugar"]      = 0.56m,
                ["confectioners sugar"] = 0.56m,

                // Dairy / fats
                ["butter"]              = 0.96m,
                ["milk"]                = 1.03m,
                ["whole milk"]          = 1.03m,
                ["heavy cream"]         = 1.01m,
                ["cream cheese"]        = 1.01m,
                ["sour cream"]          = 1.01m,
                ["yogurt"]              = 1.03m,

                // Oils / liquids
                ["water"]               = 1.00m,
                ["oil"]                 = 0.92m,
                ["vegetable oil"]       = 0.92m,
                ["olive oil"]           = 0.92m,
                ["canola oil"]          = 0.92m,
                ["honey"]               = 1.42m,
                ["maple syrup"]         = 1.32m,
                ["corn syrup"]          = 1.38m,

                // Grains
                ["rice"]                = 0.85m,
                ["oats"]                = 0.35m,
                ["rolled oats"]         = 0.35m,
                ["cornmeal"]            = 0.65m,

                // Baking
                ["cocoa powder"]        = 0.45m,
                ["baking powder"]       = 0.90m,
                ["baking soda"]         = 0.90m,
                ["cornstarch"]          = 0.54m,

                // Cheese
                ["shredded cheese"]     = 0.45m,
                ["cheese"]              = 0.45m,
                ["parmesan"]            = 0.70m,

                // Seasonings
                ["salt"]                = 1.22m,
                ["kosher salt"]         = 1.12m,
            };

        /// <summary>
        /// Look up the approximate density (g/ml) for an ingredient name.
        /// Tries exact match first, then substring match.
        /// Returns null if no match found.
        /// </summary>
        public static decimal? GetDensity(string? ingredientName)
        {
            if (string.IsNullOrWhiteSpace(ingredientName))
                return null;

            var name = ingredientName.Trim();

            // Exact match
            if (Densities.TryGetValue(name, out var density))
                return density;

            // Substring match — try longer keys first for specificity
            foreach (var kvp in Densities.OrderByDescending(k => k.Key.Length))
            {
                if (name.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            return null;
        }

        /// <summary>
        /// Convert a volume amount (in teaspoons, the volume base unit) to grams
        /// using the given density in grams per milliliter.
        /// </summary>
        public static decimal VolumeToGrams(decimal teaspoons, decimal densityGramsPerMl)
        {
            decimal ml = teaspoons * MlPerTeaspoon;
            return ml * densityGramsPerMl;
        }

        /// <summary>
        /// Convert a weight amount (in grams, the weight base unit) to teaspoons
        /// using the given density in grams per milliliter.
        /// </summary>
        public static decimal GramsToVolume(decimal grams, decimal densityGramsPerMl)
        {
            if (densityGramsPerMl == 0) return 0;
            decimal ml = grams / densityGramsPerMl;
            return ml / MlPerTeaspoon;
        }
    }
}
