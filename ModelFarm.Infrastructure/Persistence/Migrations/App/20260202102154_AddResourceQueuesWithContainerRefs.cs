using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddResourceQueuesWithContainerRefs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "queue_id",
                schema: "app",
                table: "training_jobs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "resource_queues",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    cpu_container_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gpu_container_id = table.Column<Guid>(type: "uuid", nullable: false),
                    max_concurrent_jobs = table.Column<int>(type: "integer", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_queues", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_resource_queues_is_default",
                schema: "app",
                table: "resource_queues",
                column: "is_default");

            migrationBuilder.CreateIndex(
                name: "ix_resource_queues_name",
                schema: "app",
                table: "resource_queues",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resource_queues",
                schema: "app");

            migrationBuilder.DropColumn(
                name: "queue_id",
                schema: "app",
                table: "training_jobs");
        }
    }
}
