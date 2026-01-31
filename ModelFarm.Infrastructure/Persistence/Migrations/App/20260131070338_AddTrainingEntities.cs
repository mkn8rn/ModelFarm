using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddTrainingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "training_configurations",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    type = table.Column<int>(type: "integer", nullable: false),
                    dataset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_type = table.Column<int>(type: "integer", nullable: false),
                    max_lags = table.Column<int>(type: "integer", nullable: false),
                    forecast_horizon = table.Column<int>(type: "integer", nullable: false),
                    hidden_layer_sizes_json = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    dropout_rate = table.Column<double>(type: "double precision", nullable: false),
                    learning_rate = table.Column<double>(type: "double precision", nullable: false),
                    batch_size = table.Column<int>(type: "integer", nullable: false),
                    max_epochs = table.Column<int>(type: "integer", nullable: false),
                    early_stopping_patience = table.Column<int>(type: "integer", nullable: false),
                    validation_split = table.Column<double>(type: "double precision", nullable: false),
                    test_split = table.Column<double>(type: "double precision", nullable: false),
                    random_seed = table.Column<int>(type: "integer", nullable: false),
                    performance_requirements_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    trading_environment_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_training_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "training_jobs",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    current_epoch = table.Column<int>(type: "integer", nullable: false),
                    total_epochs = table.Column<int>(type: "integer", nullable: false),
                    training_loss = table.Column<double>(type: "double precision", nullable: true),
                    validation_loss = table.Column<double>(type: "double precision", nullable: true),
                    best_validation_loss = table.Column<double>(type: "double precision", nullable: true),
                    epochs_since_improvement = table.Column<int>(type: "integer", nullable: false),
                    current_attempt = table.Column<int>(type: "integer", nullable: false),
                    max_attempts = table.Column<int>(type: "integer", nullable: false),
                    is_paused = table.Column<bool>(type: "boolean", nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    execution_options_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    overrides_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    result_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_training_jobs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_training_configurations_created_at_utc",
                schema: "app",
                table: "training_configurations",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_training_configurations_dataset_id",
                schema: "app",
                table: "training_configurations",
                column: "dataset_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_configurations_name",
                schema: "app",
                table: "training_configurations",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_training_jobs_configuration_id",
                schema: "app",
                table: "training_jobs",
                column: "configuration_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_jobs_created_at_utc",
                schema: "app",
                table: "training_jobs",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_training_jobs_status",
                schema: "app",
                table: "training_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_training_jobs_status_created_at_utc",
                schema: "app",
                table: "training_jobs",
                columns: new[] { "status", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "training_configurations",
                schema: "app");

            migrationBuilder.DropTable(
                name: "training_jobs",
                schema: "app");
        }
    }
}
