using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class Progress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "app");

            migrationBuilder.CreateTable(
                name: "background_tasks",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    progress_percent = table.Column<int>(type: "integer", nullable: false),
                    progress_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    progress_current = table.Column<long>(type: "bigint", nullable: false),
                    progress_total = table.Column<long>(type: "bigint", nullable: false),
                    parameters_json = table.Column<string>(type: "text", nullable: false),
                    result_json = table.Column<string>(type: "text", nullable: true),
                    related_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_background_tasks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_background_tasks_related_entity_id",
                schema: "app",
                table: "background_tasks",
                column: "related_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_background_tasks_status",
                schema: "app",
                table: "background_tasks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_background_tasks_status_priority_created_at_utc",
                schema: "app",
                table: "background_tasks",
                columns: new[] { "status", "priority", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "background_tasks",
                schema: "app");
        }
    }
}
