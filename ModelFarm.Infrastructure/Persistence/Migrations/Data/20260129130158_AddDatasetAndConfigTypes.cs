using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class AddDatasetAndConfigTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "type",
                schema: "data",
                table: "datasets",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "type",
                schema: "data",
                table: "datasets");
        }
    }
}
