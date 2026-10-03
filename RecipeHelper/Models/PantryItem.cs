namespace RecipeHelper.Models
{
    // A staple the user already keeps at home. The ingredient review page's
    // "Uncheck pantry" button unchecks any ingredient matching one of these, by linked
    // Kroger UPC first, then whole-word name (see PantryMatcher).
    // TODO(#1c): scope by HouseholdId once data is per-household.
    public class PantryItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";            // as typed
        public string NormalizedName { get; set; } = "";  // trimmed + lowercase; unique
        public string? KrogerUpc { get; set; }
    }
}
