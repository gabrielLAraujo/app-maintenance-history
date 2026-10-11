using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garage.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    make = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    model = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    version = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    generation = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    fuel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nickname = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    plate = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    vin = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    catalog_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    catalog_fipe_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    catalog_fipe_year_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    current_odometer = table.Column<int>(type: "integer", nullable: false),
                    odometer_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    manufacture_year = table.Column<int>(type: "integer", nullable: false),
                    model_year = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicles", x => x.id);
                    table.ForeignKey(
                        name: "fk_vehicles_asp_net_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "asp_net_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_owner_id",
                table: "vehicles",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicles");
        }
    }
}
