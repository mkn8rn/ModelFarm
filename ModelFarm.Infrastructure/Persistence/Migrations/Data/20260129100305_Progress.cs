using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.Data
{
    /// <inheritdoc />
    public partial class Progress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "data");

            migrationBuilder.CreateTable(
                name: "datasets",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    exchange = table.Column<int>(type: "integer", nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    interval = table.Column<int>(type: "integer", nullable: false),
                    start_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    record_count = table.Column<int>(type: "integer", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ingestion_operation_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_datasets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "klines",
                schema: "data",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    exchange = table.Column<int>(type: "integer", nullable: false),
                    symbol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    interval = table.Column<int>(type: "integer", nullable: false),
                    open_time = table.Column<long>(type: "bigint", nullable: false),
                    close_time = table.Column<long>(type: "bigint", nullable: false),
                    open = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    high = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    low = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    close = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    volume = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    quote_asset_volume = table.Column<decimal>(type: "numeric(28,8)", precision: 28, scale: 8, nullable: false),
                    number_of_trades = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_klines", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_datasets_name",
                schema: "data",
                table: "datasets",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_klines_exchange_symbol_interval_open_time",
                schema: "data",
                table: "klines",
                columns: new[] { "exchange", "symbol", "interval", "open_time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_klines_exchange_symbol_interval_open_time_close_time",
                schema: "data",
                table: "klines",
                columns: new[] { "exchange", "symbol", "interval", "open_time", "close_time" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "datasets",
                schema: "data");

            migrationBuilder.DropTable(
                name: "klines",
                schema: "data");
        }
    }
}
