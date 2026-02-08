namespace RecipeHelper.Utility
{
    public enum MeasureUnit
    {
        // Volume (base = teaspoons)
        Teaspoon,
        Tablespoon,
        FluidOunce,
        Cup,
        Pint,
        Quart,
        Gallon,
        Milliliter,
        Liter,

        // Weight (base = grams)
        Milligram,
        Gram,
        Kilogram,
        Ounce,
        Pound,

        // Countable
        Unit,

        // Unrecognized
        Unknown
    }

    public enum MeasureDimension
    {
        Volume,
        Weight,
        Count,
        Unknown
    }

    public static class UnitConverter
    {
        private static readonly Dictionary<string, MeasureUnit> Aliases =
            new(StringComparer.OrdinalIgnoreCase)
            {
                // Volume — abbreviations
                { "tsp", MeasureUnit.Teaspoon },
                { "tsps", MeasureUnit.Teaspoon },
                { "teaspoon", MeasureUnit.Teaspoon },
                { "teaspoons", MeasureUnit.Teaspoon },

                { "tbsp", MeasureUnit.Tablespoon },
                { "tbsps", MeasureUnit.Tablespoon },
                { "tablespoon", MeasureUnit.Tablespoon },
                { "tablespoons", MeasureUnit.Tablespoon },

                { "fl oz", MeasureUnit.FluidOunce },
                { "floz", MeasureUnit.FluidOunce },
                { "fluid ounce", MeasureUnit.FluidOunce },
                { "fluid ounces", MeasureUnit.FluidOunce },

                { "cup", MeasureUnit.Cup },
                { "cups", MeasureUnit.Cup },
                { "cup us", MeasureUnit.Cup },

                { "pt", MeasureUnit.Pint },
                { "pint", MeasureUnit.Pint },
                { "pints", MeasureUnit.Pint },

                { "qt", MeasureUnit.Quart },
                { "quart", MeasureUnit.Quart },
                { "quarts", MeasureUnit.Quart },

                { "gal", MeasureUnit.Gallon },
                { "gallon", MeasureUnit.Gallon },
                { "gallons", MeasureUnit.Gallon },

                { "ml", MeasureUnit.Milliliter },
                { "milliliter", MeasureUnit.Milliliter },
                { "milliliters", MeasureUnit.Milliliter },
                { "millilitre", MeasureUnit.Milliliter },
                { "millilitres", MeasureUnit.Milliliter },

                { "l", MeasureUnit.Liter },
                { "liter", MeasureUnit.Liter },
                { "liters", MeasureUnit.Liter },

                // Weight — abbreviations
                { "mg", MeasureUnit.Milligram },
                { "milligram", MeasureUnit.Milligram },
                { "milligrams", MeasureUnit.Milligram },

                { "g", MeasureUnit.Gram },
                { "gram", MeasureUnit.Gram },
                { "grams", MeasureUnit.Gram },

                { "kg", MeasureUnit.Kilogram },
                { "kilogram", MeasureUnit.Kilogram },
                { "kilograms", MeasureUnit.Kilogram },

                { "oz", MeasureUnit.Ounce },
                { "ounce", MeasureUnit.Ounce },
                { "ounces", MeasureUnit.Ounce },

                { "lb", MeasureUnit.Pound },
                { "lbs", MeasureUnit.Pound },
                { "pound", MeasureUnit.Pound },
                { "pounds", MeasureUnit.Pound },

                // Count
                { "unit", MeasureUnit.Unit },
                { "units", MeasureUnit.Unit },
                { "piece", MeasureUnit.Unit },
                { "pieces", MeasureUnit.Unit },
                { "count", MeasureUnit.Unit },
                { "ct", MeasureUnit.Unit },
                { "each", MeasureUnit.Unit },
                { "ea", MeasureUnit.Unit },
            };

        // Base unit for volume = teaspoon, for weight = gram
        private static readonly Dictionary<MeasureUnit, (MeasureDimension Dim, decimal Factor)> UnitInfo =
            new()
            {
                // Volume → teaspoons
                { MeasureUnit.Teaspoon,   (MeasureDimension.Volume, 1m) },
                { MeasureUnit.Tablespoon, (MeasureDimension.Volume, 3m) },
                { MeasureUnit.FluidOunce, (MeasureDimension.Volume, 6m) },
                { MeasureUnit.Cup,        (MeasureDimension.Volume, 48m) },
                { MeasureUnit.Pint,       (MeasureDimension.Volume, 96m) },
                { MeasureUnit.Quart,      (MeasureDimension.Volume, 192m) },
                { MeasureUnit.Gallon,     (MeasureDimension.Volume, 768m) },
                { MeasureUnit.Milliliter, (MeasureDimension.Volume, 0.202884m) },
                { MeasureUnit.Liter,      (MeasureDimension.Volume, 202.884m) },

                // Weight → grams
                { MeasureUnit.Milligram,  (MeasureDimension.Weight, 0.001m) },
                { MeasureUnit.Gram,       (MeasureDimension.Weight, 1m) },
                { MeasureUnit.Kilogram,   (MeasureDimension.Weight, 1000m) },
                { MeasureUnit.Ounce,      (MeasureDimension.Weight, 28.3495m) },
                { MeasureUnit.Pound,      (MeasureDimension.Weight, 453.592m) },

                // Count
                { MeasureUnit.Unit,       (MeasureDimension.Count, 1m) },

                // Unknown
                { MeasureUnit.Unknown,    (MeasureDimension.Unknown, 0m) },
            };

        /// <summary>
        /// Parse any unit string (from DB Measurement.Name, Kroger size parser, or raw recipe text)
        /// into a canonical MeasureUnit.
        /// </summary>
        public static MeasureUnit Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return MeasureUnit.Unknown;

            var trimmed = raw.Trim();
            if (Aliases.TryGetValue(trimmed, out var unit))
                return unit;

            return MeasureUnit.Unknown;
        }

        /// <summary>
        /// Get the dimension (Volume, Weight, Count) for a MeasureUnit.
        /// </summary>
        public static MeasureDimension GetDimension(MeasureUnit unit)
        {
            return UnitInfo.TryGetValue(unit, out var info) ? info.Dim : MeasureDimension.Unknown;
        }

        /// <summary>
        /// Convert a quantity to the base unit for its dimension
        /// (teaspoons for volume, grams for weight, raw value for count).
        /// Returns null if the unit is Unknown.
        /// </summary>
        public static decimal? ToBase(decimal qty, MeasureUnit unit)
        {
            if (!UnitInfo.TryGetValue(unit, out var info) || info.Dim == MeasureDimension.Unknown)
                return null;

            return qty * info.Factor;
        }

        /// <summary>
        /// Convert a quantity from one unit to another within the same dimension.
        /// Returns null if the units are in different dimensions or unrecognized.
        /// </summary>
        public static decimal? Convert(decimal qty, MeasureUnit from, MeasureUnit to)
        {
            if (!UnitInfo.TryGetValue(from, out var fromInfo) || !UnitInfo.TryGetValue(to, out var toInfo))
                return null;

            if (fromInfo.Dim != toInfo.Dim || fromInfo.Dim == MeasureDimension.Unknown)
                return null;

            if (toInfo.Factor == 0)
                return null;

            return qty * fromInfo.Factor / toInfo.Factor;
        }

        /// <summary>
        /// Returns the DB Measurement.Name-compatible display name for a MeasureUnit.
        /// </summary>
        public static string ToDisplayName(MeasureUnit unit)
        {
            return unit switch
            {
                MeasureUnit.Teaspoon   => "Teaspoons",
                MeasureUnit.Tablespoon => "Tablespoons",
                MeasureUnit.FluidOunce => "Fluid Ounces",
                MeasureUnit.Cup        => "Cups",
                MeasureUnit.Pint       => "Pints",
                MeasureUnit.Quart      => "Quarts",
                MeasureUnit.Gallon     => "Gallons",
                MeasureUnit.Milliliter => "Milliliters",
                MeasureUnit.Liter      => "Liters",
                MeasureUnit.Milligram  => "Milligrams",
                MeasureUnit.Gram       => "Grams",
                MeasureUnit.Kilogram   => "Kilograms",
                MeasureUnit.Ounce      => "Ounces",
                MeasureUnit.Pound      => "Pounds",
                MeasureUnit.Unit       => "Unit",
                _                      => "Unit",
            };
        }

        /// <summary>
        /// Pick the most human-friendly volume unit to display a quantity given in teaspoons.
        /// </summary>
        public static (decimal Qty, string DisplayName) PickBestVolumeDisplay(decimal teaspoons)
        {
            if (teaspoons >= 768m)
                return (Math.Round(teaspoons / 768m, 2), "Gallons");
            if (teaspoons >= 192m)
                return (Math.Round(teaspoons / 192m, 2), "Quarts");
            if (teaspoons >= 96m)
                return (Math.Round(teaspoons / 96m, 2), "Pints");
            if (teaspoons >= 48m)
                return (Math.Round(teaspoons / 48m, 2), "Cups");
            if (teaspoons >= 3m)
                return (Math.Round(teaspoons / 3m, 2), "Tablespoons");
            return (Math.Round(teaspoons, 2), "Teaspoons");
        }

        /// <summary>
        /// Pick the most human-friendly weight unit to display a quantity given in grams.
        /// </summary>
        public static (decimal Qty, string DisplayName) PickBestWeightDisplay(decimal grams)
        {
            if (grams >= 453.592m)
                return (Math.Round(grams / 453.592m, 2), "Pounds");
            if (grams >= 28.3495m)
                return (Math.Round(grams / 28.3495m, 2), "Ounces");
            return (Math.Round(grams, 2), "Grams");
        }
    }
}
