using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garage.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDemoAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "demo_expires_at",
                table: "asp_net_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_demo",
                table: "asp_net_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_asp_net_users_demo_expires_at",
                table: "asp_net_users",
                column: "demo_expires_at",
                filter: "is_demo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_asp_net_users_demo_expires_at",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "demo_expires_at",
                table: "asp_net_users");

            migrationBuilder.DropColumn(
                name: "is_demo",
                table: "asp_net_users");
        }
    }
}
