using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class RamResource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "max_job_duration",
                schema: "app",
                table: "resource_queues",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "max_queue_wait_time",
                schema: "app",
                table: "resource_queues",
                type: "interval",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ram_container_id",
                schema: "app",
                table: "resource_queues",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "max_capacity",
                schema: "app",
                table: "resource_containers",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_job_duration",
                schema: "app",
                table: "resource_queues");

            migrationBuilder.DropColumn(
                name: "max_queue_wait_time",
                schema: "app",
                table: "resource_queues");

            migrationBuilder.DropColumn(
                name: "ram_container_id",
                schema: "app",
                table: "resource_queues");

            migrationBuilder.AlterColumn<int>(
                name: "max_capacity",
                schema: "app",
                table: "resource_containers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
