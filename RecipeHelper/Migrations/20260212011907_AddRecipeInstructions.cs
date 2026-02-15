using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safe cleanup of legacy tables/columns that may already be gone
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_RecipeIngredient_Products_ProductId')
                    ALTER TABLE [RecipeIngredient] DROP CONSTRAINT [FK_RecipeIngredient_Products_ProductId];
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RecipeIngredient_ProductId')
                    DROP INDEX [IX_RecipeIngredient_ProductId] ON [RecipeIngredient];
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('RecipeIngredient') AND name = 'ProductId')
                    ALTER TABLE [RecipeIngredient] DROP COLUMN [ProductId];
            ");

            migrationBuilder.Sql(@"
                IF OBJECT_ID('DraftRecipes', 'U') IS NOT NULL DROP TABLE [DraftRecipes];
            ");

            migrationBuilder.Sql(@"
                DECLARE @sql NVARCHAR(MAX) = '';
                SELECT @sql += 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(parent_object_id) + '].[' + OBJECT_NAME(parent_object_id) + '] DROP CONSTRAINT [' + name + ']; '
                FROM sys.foreign_keys
                WHERE referenced_object_id = OBJECT_ID('Products');
                EXEC sp_executesql @sql;
                IF OBJECT_ID('Products', 'U') IS NOT NULL DROP TABLE [Products];
            ");

            migrationBuilder.AddColumn<string>(
                name: "Instructions",
                table: "Recipes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Instructions",
                table: "Recipes");

            migrationBuilder.AddColumn<int>(
                name: "ProductId",
                table: "RecipeIngredient",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DraftRecipes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImageUri = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PublishedRecipeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DraftRecipes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Upc = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredient_ProductId",
                table: "RecipeIngredient",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeIngredient_Products_ProductId",
                table: "RecipeIngredient",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id");
        }
    }
}
