using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddCheckpointFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "accumulated_training_ticks",
                schema: "app",
                table: "training_jobs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "has_checkpoint",
                schema: "app",
                table: "training_jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_checkpoint_at_utc",
                schema: "app",
                table: "training_jobs",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "accumulated_training_ticks",
                schema: "app",
                table: "training_jobs");

            migrationBuilder.DropColumn(
                name: "has_checkpoint",
                schema: "app",
                table: "training_jobs");

            migrationBuilder.DropColumn(
                name: "last_checkpoint_at_utc",
                schema: "app",
                table: "training_jobs");
        }
    }
}
