using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModelFarm.Infrastructure.Persistence.Migrations.App
{
    /// <inheritdoc />
    public partial class AddUserAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_access",
                schema: "app",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    accessed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_access", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_access_accessed_at_utc",
                schema: "app",
                table: "user_access",
                column: "accessed_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_user_access_user_id",
                schema: "app",
                table: "user_access",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_access",
                schema: "app");
        }
    }
}
