using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddModelTests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "model_tests",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    model_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dataset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    model_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    dataset_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    symbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    result_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_model_tests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_model_tests_created_at_utc",
                schema: "app",
                table: "model_tests",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_model_tests_dataset_id",
                schema: "app",
                table: "model_tests",
                column: "dataset_id");

            migrationBuilder.CreateIndex(
                name: "ix_model_tests_model_job_id",
                schema: "app",
                table: "model_tests",
                column: "model_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_model_tests_status",
                schema: "app",
                table: "model_tests",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "model_tests",
                schema: "app");
        }
    }
}
