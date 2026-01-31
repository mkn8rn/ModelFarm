using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class RemoveJobOverridesAddUseEarlyStopping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "execution_options_json",
                schema: "app",
                table: "training_jobs");

            migrationBuilder.DropColumn(
                name: "overrides_json",
                schema: "app",
                table: "training_jobs");

            migrationBuilder.AddColumn<bool>(
                name: "use_early_stopping",
                schema: "app",
                table: "training_configurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "use_early_stopping",
                schema: "app",
                table: "training_configurations");

            migrationBuilder.AddColumn<string>(
                name: "execution_options_json",
                schema: "app",
                table: "training_jobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "overrides_json",
                schema: "app",
                table: "training_jobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }
    }
}
