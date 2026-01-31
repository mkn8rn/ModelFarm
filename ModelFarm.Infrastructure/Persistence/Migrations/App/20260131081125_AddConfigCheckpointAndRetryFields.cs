using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddConfigCheckpointAndRetryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "checkpoint_interval_epochs",
                schema: "app",
                table: "training_configurations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "learning_rate_retry_scale",
                schema: "app",
                table: "training_configurations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "max_retry_attempts",
                schema: "app",
                table: "training_configurations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "retry_until_success",
                schema: "app",
                table: "training_configurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "save_checkpoints",
                schema: "app",
                table: "training_configurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "scale_learning_rate_on_retry",
                schema: "app",
                table: "training_configurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "shuffle_on_retry",
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
                name: "checkpoint_interval_epochs",
                schema: "app",
                table: "training_configurations");

            migrationBuilder.DropColumn(
                name: "learning_rate_retry_scale",
                schema: "app",
                table: "training_configurations");

            migrationBuilder.DropColumn(
                name: "max_retry_attempts",
                schema: "app",
                table: "training_configurations");

            migrationBuilder.DropColumn(
                name: "retry_until_success",
                schema: "app",
                table: "training_configurations");

            migrationBuilder.DropColumn(
                name: "save_checkpoints",
                schema: "app",
                table: "training_configurations");

            migrationBuilder.DropColumn(
                name: "scale_learning_rate_on_retry",
                schema: "app",
                table: "training_configurations");

            migrationBuilder.DropColumn(
                name: "shuffle_on_retry",
                schema: "app",
                table: "training_configurations");
        }
    }
}
