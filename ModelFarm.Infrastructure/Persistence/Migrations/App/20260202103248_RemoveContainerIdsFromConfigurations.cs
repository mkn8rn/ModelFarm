using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class RemoveContainerIdsFromConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cpu_container_id",
                schema: "app",
                table: "training_configurations");

            migrationBuilder.DropColumn(
                name: "gpu_container_id",
                schema: "app",
                table: "training_configurations");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
