using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class IngredientTableMiddleWare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CanonicalName = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DefaultDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_CanonicalName",
                table: "Ingredients",
                column: "CanonicalName",
                unique: true);

            migrationBuilder.CreateTable(
                name: "KrogerProducts",
                columns: table => new
                {
                    Upc = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KrogerProducts", x => x.Upc);
                });

            migrationBuilder.CreateTable(
                name: "IngredientKrogerProducts",
                columns: table => new
                {
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    Upc = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    MatchMethod = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngredientKrogerProducts", x => new { x.IngredientId, x.Upc });

                    table.ForeignKey(
                        name: "FK_IngredientKrogerProducts_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "FK_IngredientKrogerProducts_KrogerProducts_Upc",
                        column: x => x.Upc,
                        principalTable: "KrogerProducts",
                        principalColumn: "Upc",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IngredientKrogerProducts_Upc",
                table: "IngredientKrogerProducts",
                column: "Upc");

            migrationBuilder.CreateTable(
                name: "RecipeIngredient",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipeId = table.Column<int>(type: "int", nullable: false),
                    IngredientId = table.Column<int>(type: "int", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    MeasurementId = table.Column<int>(type: "int", nullable: true),
                    SelectedKrogerUpc = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeIngredient", x => x.Id);

                    table.ForeignKey(
                        name: "FK_RecipeIngredient_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);

                    table.ForeignKey(
                        name: "FK_RecipeIngredient_KrogerProducts_SelectedKrogerUpc",
                        column: x => x.SelectedKrogerUpc,
                        principalTable: "KrogerProducts",
                        principalColumn: "Upc");

                    table.ForeignKey(
                        name: "FK_RecipeIngredient_Measurements_MeasurementId",
                        column: x => x.MeasurementId,
                        principalTable: "Measurements",
                        principalColumn: "Id");

                    table.ForeignKey(
                        name: "FK_RecipeIngredient_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredient_IngredientId",
                table: "RecipeIngredient",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredient_MeasurementId",
                table: "RecipeIngredient",
                column: "MeasurementId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredient_RecipeId",
                table: "RecipeIngredient",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeIngredient_SelectedKrogerUpc",
                table: "RecipeIngredient",
                column: "SelectedKrogerUpc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "IngredientKrogerProducts");
            migrationBuilder.DropTable(name: "RecipeIngredient");
            migrationBuilder.DropTable(name: "KrogerProducts");
            migrationBuilder.DropTable(name: "Ingredients");
        }
    }
}
