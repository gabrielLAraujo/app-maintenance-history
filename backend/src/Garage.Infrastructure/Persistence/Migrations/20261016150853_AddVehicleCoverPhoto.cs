using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garage.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleCoverPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cover_photo_id",
                table: "vehicles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_cover_photo_id",
                table: "vehicles",
                column: "cover_photo_id");

            migrationBuilder.AddForeignKey(
                name: "fk_vehicles_attachments_cover_photo_id",
                table: "vehicles",
                column: "cover_photo_id",
                principalTable: "attachments",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_vehicles_attachments_cover_photo_id",
                table: "vehicles");

            migrationBuilder.DropIndex(
                name: "ix_vehicles_cover_photo_id",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "cover_photo_id",
                table: "vehicles");
        }
    }
}
