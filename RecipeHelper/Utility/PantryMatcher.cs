using System.Text.RegularExpressions;
using RecipeHelper.Models;

namespace RecipeHelper.Utility
{
    public static class PantryMatcher
    {
        public static string Normalize(string? name) => (name ?? "").Trim().ToLowerInvariant();

        // The user's pantry list plus the built-in defaults (PantryDefaults).
        public static bool IsPantryOrDefault(string? name, string? upc, IReadOnlyCollection<PantryItem> userItems) =>
            IsPantry(name, upc, userItems) || IsPantry(name, null, PantryDefaults.Items);

        // UPC match wins; otherwise whole-word match on the end of the name ("oil" no
        // longer matches "foil").
        public static bool IsPantry(string? name, string? upc, IReadOnlyCollection<PantryItem> items)
        {
            if (items.Count == 0) return false;

            if (!string.IsNullOrWhiteSpace(upc) &&
                items.Any(p => !string.IsNullOrWhiteSpace(p.KrogerUpc) &&
                               string.Equals(p.KrogerUpc.Trim(), upc.Trim(), StringComparison.OrdinalIgnoreCase)))
                return true;

            // Judge only the ingredient itself: drop "(to taste)" notes and anything after
            // a comma ("onion, finely chopped"). The pantry item must then be the *end*
            // of the name -- what the ingredient is -- so "pepper" matches "black pepper"
            // but not "red bell pepper" (unless "pepper" itself is listed), and "flour"
            // matches "all-purpose flour" but not "flour tortillas".
            // Two readings of the name: the ingredient alone, and the whole text with the
            // notes folded back in, so "garlic, minced" / "Garlic Cloves (Minced)" can
            // match a "garlic minced" item.
            var candidates = new[]
            {
                Tokenize(HeadPhrase(name)),
                Tokenize(Regex.Replace(name ?? "", @"[(),]", " "))
            };

            foreach (var words in candidates)
            {
                if (words.Length == 0) continue;
                foreach (var item in items)
                {
                    var needle = Tokenize(item.Name);
                    if (needle.Length == 0 || needle.Length > words.Length) continue;
                    int offset = words.Length - needle.Length;
                    int k = 0;
                    while (k < needle.Length && words[offset + k] == needle[k]) k++;
                    if (k == needle.Length) return true;
                }
            }
            return false;
        }

        private static string HeadPhrase(string? name)
        {
            var text = Regex.Replace(name ?? "", @"\([^)]*\)", " ");
            var comma = text.IndexOf(',');
            return comma >= 0 ? text[..comma] : text;
        }

        // Lowercase words, with a trailing plural "s" trimmed so "eggs" == "egg".
        private static string[] Tokenize(string? text) =>
            Regex.Matches((text ?? "").ToLowerInvariant(), "[a-z0-9]+")
                .Select(m => m.Value.Length > 3 && m.Value.EndsWith('s') && !m.Value.EndsWith("ss")
                    ? m.Value[..^1] : m.Value)
                .ToArray();
    }
}
