using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeHelper.Migrations
{
    /// <inheritdoc />
    public partial class measurement_table : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MeasurementId",
                table: "RecipeProducts",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Measurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Measurements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeProducts_MeasurementId",
                table: "RecipeProducts",
                column: "MeasurementId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeProducts_Measurements_MeasurementId",
                table: "RecipeProducts",
                column: "MeasurementId",
                principalTable: "Measurements",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeProducts_Measurements_MeasurementId",
                table: "RecipeProducts");

            migrationBuilder.DropTable(
                name: "Measurements");

            migrationBuilder.DropIndex(
                name: "IX_RecipeProducts_MeasurementId",
                table: "RecipeProducts");

            migrationBuilder.DropColumn(
                name: "MeasurementId",
                table: "RecipeProducts");
        }
    }
}
