using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddUserOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_ownerships",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_ownerships", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_ownerships_resource_type_resource_id",
                schema: "app",
                table: "user_ownerships",
                columns: new[] { "resource_type", "resource_id" });

            migrationBuilder.CreateIndex(
                name: "ix_user_ownerships_user_id",
                schema: "app",
                table: "user_ownerships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_ownerships_user_id_resource_type",
                schema: "app",
                table: "user_ownerships",
                columns: new[] { "user_id", "resource_type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_ownerships",
                schema: "app");
        }
    }
}
