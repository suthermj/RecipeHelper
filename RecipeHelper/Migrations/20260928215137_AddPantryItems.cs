using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class AddPantryItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PantryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    KrogerUpc = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PantryItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PantryItems_NormalizedName",
                table: "PantryItems",
                column: "NormalizedName",
                unique: true);

            // Starter list: the keywords the review page used to hardcode.
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('olive oil', 'olive oil')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('vegetable oil', 'vegetable oil')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('canola oil', 'canola oil')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('cooking oil', 'cooking oil')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('oil', 'oil')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('salt', 'salt')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('pepper', 'pepper')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('black pepper', 'black pepper')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('garlic powder', 'garlic powder')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('onion powder', 'onion powder')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('baking powder', 'baking powder')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('baking soda', 'baking soda')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('brown sugar', 'brown sugar')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('sugar', 'sugar')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('flour', 'flour')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('cornstarch', 'cornstarch')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('vinegar', 'vinegar')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('soy sauce', 'soy sauce')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('butter', 'butter')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('water', 'water')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('paprika', 'paprika')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('cumin', 'cumin')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('oregano', 'oregano')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('basil', 'basil')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('thyme', 'thyme')");
            migrationBuilder.Sql("INSERT INTO PantryItems (Name, NormalizedName) VALUES ('cinnamon', 'cinnamon')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PantryItems");
        }
    }
}
