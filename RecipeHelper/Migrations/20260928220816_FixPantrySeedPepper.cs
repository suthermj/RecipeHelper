using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class FixPantrySeedPepper : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bare "pepper" also caught bell/Anaheim peppers; keep the seasoning forms only.
            migrationBuilder.Sql("DELETE FROM PantryItems WHERE NormalizedName = 'pepper'");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('ground pepper', 'ground pepper')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('salt and pepper', 'salt and pepper')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM PantryItems WHERE NormalizedName IN ('ground pepper', 'salt and pepper')");
            migrationBuilder.Sql("IF NOT EXISTS (SELECT 1 FROM PantryItems WHERE NormalizedName = 'pepper') INSERT INTO PantryItems (Name, NormalizedName) VALUES ('pepper', 'pepper')");
        }
    }
}
