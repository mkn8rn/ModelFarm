using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class FixResourceContainerNameIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_resource_containers_name",
                schema: "app",
                table: "resource_containers");

            migrationBuilder.CreateIndex(
                name: "ix_resource_containers_name_type",
                schema: "app",
                table: "resource_containers",
                columns: new[] { "name", "type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_resource_containers_name_type",
                schema: "app",
                table: "resource_containers");

            migrationBuilder.CreateIndex(
                name: "ix_resource_containers_name",
                schema: "app",
                table: "resource_containers",
                column: "name",
                unique: true);
        }
    }
}
