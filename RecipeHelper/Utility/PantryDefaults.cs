namespace RecipeHelper.Utility
{
    // Staples that "Uncheck pantry" always skips, for everyone. The user's own additions
    // live in the PantryItems table (Pantry page). Matching rules are in PantryMatcher:
    // the name must *end* with one of these, so entries are chosen to avoid catching
    // fresh produce ("ground cloves", not "cloves"; no bare "pepper").
    public static class PantryDefaults
    {
        public static readonly IReadOnlyList<string> Names = new[]
        {
            // Oils, vinegars, condiments
            "oil", "olive oil", "vegetable oil", "canola oil", "cooking oil", "cooking spray",
            "vinegar", "soy sauce", "worcestershire sauce", "fish sauce", "hot sauce",
            "ketchup", "mustard", "mayonnaise",
            // Salt, pepper, spices, dried herbs
            "salt", "black pepper", "ground pepper", "white pepper", "cayenne pepper",
            "salt and pepper", "red pepper flakes", "crushed red pepper",
            "garlic powder", "onion powder", "minced garlic", "garlic minced", "garlic cloves minced",
            "paprika", "cumin", "oregano", "basil", "thyme", "cinnamon", "chili powder",
            "ground ginger", "nutmeg", "ground cloves", "turmeric", "ground coriander",
            "curry powder", "italian seasoning", "taco seasoning", "bay leaves", "rosemary",
            "sage", "parsley flakes", "dill weed", "vanilla extract",
            // Baking
            "baking powder", "baking soda", "sugar", "brown sugar", "flour", "cornstarch",
            "honey", "cocoa powder", "yeast", "cream of tartar",
            // Other
            "butter", "water",
        };

        public static readonly IReadOnlyList<Models.PantryItem> Items = Names
            .Select(n => new Models.PantryItem { Name = n, NormalizedName = PantryMatcher.Normalize(n) })
            .ToList();
    }
}
