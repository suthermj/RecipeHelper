using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class MovePantrySeedToBuiltIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The starter list moved into code (PantryDefaults). Drop the seeded rows so the
            // table holds only the user's own additions.
            migrationBuilder.Sql("DELETE FROM PantryItems WHERE NormalizedName IN ('oil', 'olive oil', 'vegetable oil', 'canola oil', 'cooking oil', 'cooking spray', 'vinegar', 'soy sauce', 'worcestershire sauce', 'fish sauce', 'hot sauce', 'ketchup', 'mustard', 'mayonnaise', 'salt', 'black pepper', 'ground pepper', 'white pepper', 'cayenne pepper', 'salt and pepper', 'red pepper flakes', 'crushed red pepper', 'garlic powder', 'onion powder', 'minced garlic', 'garlic minced', 'garlic cloves minced', 'paprika', 'cumin', 'oregano', 'basil', 'thyme', 'cinnamon', 'chili powder', 'ground ginger', 'nutmeg', 'ground cloves', 'turmeric', 'ground coriander', 'curry powder', 'italian seasoning', 'taco seasoning', 'bay leaves', 'rosemary', 'sage', 'parsley flakes', 'dill weed', 'vanilla extract', 'baking powder', 'baking soda', 'sugar', 'brown sugar', 'flour', 'cornstarch', 'honey', 'cocoa powder', 'yeast', 'cream of tartar', 'butter', 'water', 'pepper')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Seeded rows are not restored; the built-in defaults still apply from code.
        }
    }
}
