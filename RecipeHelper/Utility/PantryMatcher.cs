using System.Text.RegularExpressions;
using RecipeHelper.Models;

namespace RecipeHelper.Utility
{
    public static class PantryMatcher
    {
        public static string Normalize(string? name) => (name ?? "").Trim().ToLowerInvariant();

        // UPC match wins; otherwise the pantry item's words must appear as a contiguous
        // run of whole words in the ingredient name ("oil" no longer matches "foil").
        public static bool IsPantry(string? name, string? upc, IReadOnlyCollection<PantryItem> items)
        {
            if (items.Count == 0) return false;

            if (!string.IsNullOrWhiteSpace(upc) &&
                items.Any(p => !string.IsNullOrWhiteSpace(p.KrogerUpc) &&
                               string.Equals(p.KrogerUpc.Trim(), upc.Trim(), StringComparison.OrdinalIgnoreCase)))
                return true;

            var words = Tokenize(name);
            if (words.Length == 0) return false;

            foreach (var item in items)
            {
                var needle = Tokenize(item.Name);
                if (needle.Length == 0 || needle.Length > words.Length) continue;
                for (int start = 0; start + needle.Length <= words.Length; start++)
                {
                    int k = 0;
                    while (k < needle.Length && words[start + k] == needle[k]) k++;
                    if (k == needle.Length) return true;
                }
            }
            return false;
        }

        // Lowercase words, with a trailing plural "s" trimmed so "eggs" == "egg".
        private static string[] Tokenize(string? text) =>
            Regex.Matches((text ?? "").ToLowerInvariant(), "[a-z0-9]+")
                .Select(m => m.Value.Length > 3 && m.Value.EndsWith('s') && !m.Value.EndsWith("ss")
                    ? m.Value[..^1] : m.Value)
                .ToArray();
    }
}
