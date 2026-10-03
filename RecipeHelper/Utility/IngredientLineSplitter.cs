using System.Text.RegularExpressions;

namespace RecipeHelper.Utility
{
    // Splits an ingredient line as written ("1 1/2 cups flour, sifted") into its leading
    // amount ("1 1/2 cups") and the rest ("flour, sifted"), so free-form lines render in
    // the same amount | name columns as rows built from Quantity/Measurement. Lines with
    // no leading amount ("Salt, to taste") return an empty amount.
    public static class IngredientLineSplitter
    {
        private const string Number = @"(?:\d+\s+\d+/\d+|\d+/\d+|\d+(?:\.\d+)?\s*[½⅓⅔¼¾⅛]?|[½⅓⅔¼¾⅛])";

        private const string Unit =
            @"(?:fl\.?\s*oz|fluid\s+ounces?|cups?|c|tablespoons?|tbsps?|tbs|teaspoons?|tsps?|ounces?|oz|pounds?|lbs?|" +
            @"grams?|g|kilograms?|kg|milliliters?|ml|liters?|l|pints?|pt|quarts?|qt|gallons?|gal|pinch(?:es)?|dash(?:es)?)\.?";

        private static readonly Regex Leading = new(
            $@"^\s*(?<amount>{Number}(?:\s*(?:-|–|to)\s*{Number})?(?:\s+{Unit}(?=\s|,|$))?)[\s,]+(?<rest>\S.*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex UnitOnly = new($"^{Unit}$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static (string Amount, string Name) Split(string? line)
        {
            line = (line ?? "").Trim();
            var m = Leading.Match(line);
            // "3 cups" -- the unit alone isn't a name; keep the line whole.
            return m.Success && !UnitOnly.IsMatch(m.Groups["rest"].Value.Trim())
                ? (Regex.Replace(m.Groups["amount"].Value, @"\s+", " ").Trim(), m.Groups["rest"].Value.Trim())
                : ("", line);
        }
    }
}
