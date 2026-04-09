using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmnilensScraping.Migrations
{
    /// <inheritdoc />
    public partial class AddPharmacyVerticalApisAndReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PharmacyLocationId",
                table: "pharmacy_reservations",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "PickupCode",
                table: "pharmacy_reservations",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryClass",
                table: "pharmacy_product_facts",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "pharmacy_reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CanonicalProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReminderType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IntervalDays = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    NextReminderAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastTriggeredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pharmacy_reminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pharmacy_reminders_canonical_products_CanonicalProductId",
                        column: x => x.CanonicalProductId,
                        principalTable: "canonical_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pharmacy_reminders_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pharmacy_reservations_PharmacyLocationId",
                table: "pharmacy_reservations",
                column: "PharmacyLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_pharmacy_reminders_CanonicalProductId",
                table: "pharmacy_reminders",
                column: "CanonicalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_pharmacy_reminders_UserId_IsActive_NextReminderAtUtc",
                table: "pharmacy_reminders",
                columns: new[] { "UserId", "IsActive", "NextReminderAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_pharmacy_reservations_pharmacy_locations_PharmacyLocationId",
                table: "pharmacy_reservations",
                column: "PharmacyLocationId",
                principalTable: "pharmacy_locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pharmacy_reservations_pharmacy_locations_PharmacyLocationId",
                table: "pharmacy_reservations");

            migrationBuilder.DropTable(
                name: "pharmacy_reminders");

            migrationBuilder.DropIndex(
                name: "IX_pharmacy_reservations_PharmacyLocationId",
                table: "pharmacy_reservations");

            migrationBuilder.DropColumn(
                name: "PharmacyLocationId",
                table: "pharmacy_reservations");

            migrationBuilder.DropColumn(
                name: "PickupCode",
                table: "pharmacy_reservations");

            migrationBuilder.DropColumn(
                name: "CategoryClass",
                table: "pharmacy_product_facts");
        }
    }
}
