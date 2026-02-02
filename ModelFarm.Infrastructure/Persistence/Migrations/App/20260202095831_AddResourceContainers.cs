using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddResourceContainers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cpu_container_id",
                schema: "app",
                table: "training_configurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "gpu_container_id",
                schema: "app",
                table: "training_configurations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "use_gpu_for_inference",
                schema: "app",
                table: "training_configurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "resource_containers",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    max_capacity = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_containers", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_resource_containers_name",
                schema: "app",
                table: "resource_containers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resource_containers_type",
                schema: "app",
                table: "resource_containers",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "ix_resource_containers_type_is_default",
                schema: "app",
                table: "resource_containers",
                columns: new[] { "type", "is_default" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resource_containers",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "cpu_container_id",
                schema: "app",
                table: "training_configurations");

            migrationBuilder.DropColumn(
                name: "gpu_container_id",
                schema: "app",
                table: "training_configurations");

            migrationBuilder.DropColumn(
                name: "use_gpu_for_inference",
                schema: "app",
                table: "training_configurations");
        }
    }
}
