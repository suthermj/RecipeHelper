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

        // Reported on prod: Red Bell Pepper, Anaheim Peppers and Flour Tortillas were
        // unchecked by "Uncheck pantry" because the item's word appeared anywhere in the
        // name. The pantry item must be what the ingredient *is* (its trailing words).
        [Theory]
        [InlineData("Red Bell Pepper (Deseeded And Chopped)", false)]
        [InlineData("Anaheim Peppers", false)]
        [InlineData("Flour Tortillas", false)]
        [InlineData("sugar snap peas", false)]
        [InlineData("water chestnuts", false)]
        [InlineData("Black Pepper", true)]
        [InlineData("freshly ground black pepper, to taste", true)]
        [InlineData("Salt And Pepper (To Taste)", true)]
        [InlineData("all-purpose flour", true)]
        public void Item_must_be_the_ingredient_not_a_modifier(string ingredient, bool expected) =>
            Assert.Equal(expected, PantryMatcher.IsPantry(ingredient, null,
                new[] { P("black pepper"), P("salt and pepper"), P("flour"), P("sugar"), P("water") }));

        // Seasoning forms of pepper stay auto-excluded via the seeded list, while fresh
        // peppers do not match anything.
        [Theory]
        [InlineData("Red Pepper Flakes", true)]
        [InlineData("crushed red pepper flakes", true)]
        [InlineData("Black Pepper", true)]
        [InlineData("cayenne pepper", true)]
        [InlineData("Red Bell Pepper (Deseeded And Chopped)", false)]
        [InlineData("Anaheim Peppers", false)]
        public void Seeded_pepper_seasonings_match_but_fresh_peppers_do_not(string ingredient, bool expected) =>
            Assert.Equal(expected, PantryMatcher.IsPantry(ingredient, null,
                new[] { P("black pepper"), P("ground pepper"), P("red pepper flakes"), P("crushed red pepper"), P("cayenne pepper"), P("white pepper") }));

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
