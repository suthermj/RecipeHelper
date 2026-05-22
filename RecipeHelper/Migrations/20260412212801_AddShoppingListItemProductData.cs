using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingListItemProductData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StoreId",
                table: "ShoppingLists",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AisleDescription",
                table: "ShoppingListItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AisleNumber",
                table: "ShoppingListItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "ShoppingListItems",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "ShoppingListItems",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PromoPrice",
                table: "ShoppingListItems",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StoreId",
                table: "ShoppingLists");

            migrationBuilder.DropColumn(
                name: "AisleDescription",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "AisleNumber",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "PromoPrice",
                table: "ShoppingListItems");
        }
    }
}
