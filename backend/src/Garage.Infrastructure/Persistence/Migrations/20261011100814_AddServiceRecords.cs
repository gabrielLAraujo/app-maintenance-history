using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Garage.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    odometer = table.Column<int>(type: "integer", nullable: true),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    performed_by = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    labor_cost = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    replaced_original_part = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_records_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_record_parts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    brand = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    part_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    quantity = table.Column<decimal>(type: "numeric(8,3)", nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    service_record_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_record_parts", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_record_parts_service_records_service_record_id",
                        column: x => x.service_record_id,
                        principalTable: "service_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_record_parts_service_record_id",
                table: "service_record_parts",
                column: "service_record_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_records_vehicle_id_date_id",
                table: "service_records",
                columns: new[] { "vehicle_id", "date", "id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_record_parts");

            migrationBuilder.DropTable(
                name: "service_records");
        }
    }
}
