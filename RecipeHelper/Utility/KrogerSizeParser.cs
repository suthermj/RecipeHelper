using System.Globalization;
using System.Net.ServerSentEvents;
using System.Text.RegularExpressions;
using RecipeHelper.Models.Kroger;

using System.Globalization;
using System.Text.RegularExpressions;
namespace RecipeHelper.Utility
{
    public sealed class ParsedPackSize
    {
        // Primary measurement (usually weight/volume for conversion)
        public decimal? PrimaryQty { get; init; }
        public string? PrimaryUnit { get; init; } // e.g. "oz", "fl oz", "lb", "gal", "ct"

        // Composite count (used when ingredient is unit-based)
        public decimal? CountEach { get; init; } // e.g. 8 for "8 ct / 22 oz"

        public bool IsComposite { get; init; }
        public PackDimension Dimension { get; init; }
        public bool ParsedOk { get; init; }

        public override string ToString()
            => $"ParsedOk={ParsedOk}, Dim={Dimension}, CountEach={CountEach}, Primary={PrimaryQty} {PrimaryUnit}";
    }

    public static class KrogerSizeParser
    {
        // Matches:
        //  - "15 oz"
        //  - "16.9 fl oz"
        //  - "1/2 gal"
        //  - "8 ct / 22 oz"
        //  - "4 ct / 16 oz"
        //  - "20 ct / 24 oz"
        private static readonly Regex CompositeRegex = new(
            @"^\s*(?<count>[\d]+(?:\.\d+)?)\s*(?<countUnit>ct|count|ea|each)\s*/\s*(?<qty>[\d]+(?:\.\d+)?|[\d]+\s*/\s*[\d]+)\s*(?<unit>fl\s*oz|oz|lb|lbs|g|gram|grams|kg|kilogram|kilograms|ml|milliliter|milliliters|l|liter|liters|pt|pint|pints|qt|quart|quarts|gal|gallon|gallons)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex SimpleRegex = new(
            @"^\s*(?<qty>[\d]+(?:\.\d+)?|[\d]+\s*/\s*[\d]+)\s*(?<unit>fl\s*oz|oz|lb|lbs|g|gram|grams|kg|kilogram|kilograms|ml|milliliter|milliliters|l|liter|liters|pt|pint|pints|qt|quart|quarts|gal|gallon|gallons|ct|count|ea|each)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static ParsedPackSize TryParse(string? size)
        {
            if (string.IsNullOrWhiteSpace(size) || size.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase))
                return new ParsedPackSize { ParsedOk = false, Dimension = PackDimension.Unknown };

            // Composite first
            var cm = CompositeRegex.Match(size);
            if (cm.Success)
            {
                var countEach = TryParseDecimal(cm.Groups["count"].Value);
                var qty = TryParseDecimalOrFraction(cm.Groups["qty"].Value);
                var unit = NormalizeSizeUnit(cm.Groups["unit"].Value);

                var dim = GetDimensionFromUnit(unit);
                // Composite means we have count + some measurable primary (weight/volume)
                return new ParsedPackSize
                {
                    ParsedOk = countEach.HasValue && qty.HasValue && !string.IsNullOrWhiteSpace(unit),
                    IsComposite = true,
                    CountEach = countEach,
                    PrimaryQty = qty,
                    PrimaryUnit = unit,
                    Dimension = PackDimension.Composite
                };
            }

            // Simple
            var sm = SimpleRegex.Match(size);
            if (sm.Success)
            {
                var qty = TryParseDecimalOrFraction(sm.Groups["qty"].Value);
                var unit = NormalizeSizeUnit(sm.Groups["unit"].Value);

                var dim = GetDimensionFromUnit(unit);

                // For unit-only sizes like "1 ct", Primary is still set (qty=1 unit=ct),
                // and Dimension=Unit.
                return new ParsedPackSize
                {
                    ParsedOk = qty.HasValue && !string.IsNullOrWhiteSpace(unit),
                    IsComposite = false,
                    CountEach = unit == "ct" ? qty : null, // optional; you can choose to NOT set this
                    PrimaryQty = qty,
                    PrimaryUnit = unit,
                    Dimension = dim
                };
            }

            // Anything else (Varies, About, etc.)
            return new ParsedPackSize { ParsedOk = false, Dimension = PackDimension.Unknown };
        }

        private static decimal? TryParseDecimal(string s)
        {
            if (decimal.TryParse(s.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var val))
                return val;
            return null;
        }

        private static decimal? TryParseDecimalOrFraction(string s)
        {
            var t = s.Trim().Replace(" ", "");
            // fraction like "1/2"
            var parts = t.Split('/');
            if (parts.Length == 2 &&
                decimal.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var num) &&
                decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var den) &&
                den != 0)
            {
                return num / den;
            }

            if (decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var val))
                return val;

            return null;
        }

        private static string NormalizeSizeUnit(string rawUnit)
        {
            var u = rawUnit.Trim().ToLowerInvariant();
            u = Regex.Replace(u, @"\s+", " "); // collapse spaces: "fl   oz" -> "fl oz"

            return u switch
            {
                "lbs" => "lb",
                "grams" => "g",
                "gram" => "g",
                "kilogram" => "kg",
                "kilograms" => "kg",
                "milliliter" => "ml",
                "milliliters" => "ml",
                "liter" => "l",
                "liters" => "l",
                "pint" => "pt",
                "pints" => "pt",
                "quart" => "qt",
                "quarts" => "qt",
                "gallon" => "gal",
                "gallons" => "gal",
                "count" => "ct",
                "ea" => "each",
                _ => u // "oz", "fl oz", "lb", "ct", "each", "qt", etc.
            };
        }

        private static PackDimension GetDimensionFromUnit(string unit)
        {
            var u = unit.ToLowerInvariant();

            if (u == "ct" || u == "each" || u == "ea")
                return PackDimension.Unit;

            if (u == "oz" || u == "lb" || u == "g" || u == "kg")
                return PackDimension.Weight;

            if (u == "fl oz" || u == "ml" || u == "l" || u == "pt" || u == "qt" || u == "gal")
                return PackDimension.Volume;

            return PackDimension.Unknown;
        }


    }

    public static class SoldByInference
    {
        public static string InferSoldBy(string? soldByRaw, string? size, IEnumerable<string>? categories)
        {
            // 1) If API gave it, trust it
            if (!string.IsNullOrWhiteSpace(soldByRaw))
            {
                var s = soldByRaw.Trim().ToUpperInvariant();
                if (s is "UNIT" or "WEIGHT")
                    return s;
            }

            // 2) If size is clearly count-based, it's UNIT
            if (!string.IsNullOrWhiteSpace(size))
            {
                var z = size.Trim().ToLowerInvariant();
                if (z.Contains(" ct") || z.EndsWith("ct") || z.Contains(" each") || z.EndsWith("each"))
                    return "UNIT";
            }

            // 3) Parse size to check weight-based sizes like "1 lb"
            var parsed = KrogerSizeParser.TryParse(size);
            var hasCategories = categories != null;
            var cat = hasCategories
                ? string.Join(" | ", categories!).ToLowerInvariant()
                : string.Empty;

            var isProduce = cat.Contains("produce");
            var isMeatSeafood = cat.Contains("meat") || cat.Contains("seafood");

            // If it's Produce/Meat and the size is in pounds/weight, it's usually WEIGHT in your data
            if ((isProduce || isMeatSeafood) && parsed.ParsedOk)
            {
                // Composite sizes are almost always packages -> UNIT
                if (parsed.IsComposite)
                    return "UNIT";

                if (parsed.Dimension == PackDimension.Weight && parsed.PrimaryUnit == "lb")
                    return "WEIGHT";
            }

            // 4) Default: UNIT (packaged goods + most items added to cart as count)
            return "UNIT";
        }
    }




}
