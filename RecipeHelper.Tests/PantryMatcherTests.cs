using RecipeHelper.Models;
using RecipeHelper.Utility;
using Xunit;

namespace RecipeHelper.Tests
{
    // The review page's "Uncheck pantry" used bare substring matching, so "oil" also
    // matched "foil" / "boiled"; matching is now whole-word, with UPC taking priority.
    public class PantryMatcherTests
    {
        private static PantryItem P(string name, string? upc = null) =>
            new() { Name = name, NormalizedName = PantryMatcher.Normalize(name), KrogerUpc = upc };

        [Theory]
        [InlineData("aluminum foil", false)]
        [InlineData("boiled eggs", false)]
        [InlineData("unsalted butter", false)]
        [InlineData("olive oil", true)]
        [InlineData("Extra Virgin Olive Oil", true)]
        public void Oil_matches_whole_words_only(string ingredient, bool expected) =>
            Assert.Equal(expected, PantryMatcher.IsPantry(ingredient, null, new[] { P("oil"), P("salt") }));

        [Fact]
        public void Multi_word_item_needs_contiguous_words() =>
            Assert.False(PantryMatcher.IsPantry("olive and garlic oil", null, new[] { P("olive oil") }));

        [Fact]
        public void Plural_is_trimmed() =>
            Assert.True(PantryMatcher.IsPantry("eggs", null, new[] { P("egg") }));

        [Fact]
        public void Upc_match_wins_over_name() =>
            Assert.True(PantryMatcher.IsPantry("something unrelated", " 0001111 ", new[] { P("Chili", "0001111") }));

        [Fact]
        public void Empty_list_matches_nothing() =>
            Assert.False(PantryMatcher.IsPantry("salt", null, Array.Empty<PantryItem>()));
    }
}
