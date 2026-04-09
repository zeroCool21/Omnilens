using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmnilensScraping.Migrations
{
    /// <inheritdoc />
    public partial class AddB2bCompanyInvitesAndBenchmark : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "company_invites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    TokenHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    AcceptedByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_invites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_company_invites_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_invites_users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_company_invites_AcceptedByUserId",
                table: "company_invites",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_company_invites_CompanyId_Email_Status",
                table: "company_invites",
                columns: new[] { "CompanyId", "Email", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_company_invites_TokenHash",
                table: "company_invites",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "company_invites");
        }
    }
}
