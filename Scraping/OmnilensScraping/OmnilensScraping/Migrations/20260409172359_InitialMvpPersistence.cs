using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmnilensScraping.Migrations
{
    /// <inheritdoc />
    public partial class InitialMvpPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "canonical_products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Brand = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CategoryName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Gtin = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CanonicalSku = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Vertical = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_products", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    VatCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RetailerCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    SupportsCatalogBootstrap = table.Column<bool>(type: "INTEGER", nullable: false),
                    SupportsLiveScrape = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    PriorityScore = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UserType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", maxLength: 2, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "canonical_product_attributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CanonicalProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AttributeName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AttributeValue = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_canonical_product_attributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_canonical_product_attributes_canonical_products_CanonicalProductId",
                        column: x => x.CanonicalProductId,
                        principalTable: "canonical_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pharmacy_product_facts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CanonicalProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ActiveIngredient = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DosageForm = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    StrengthText = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    PackageSize = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    RequiresPrescription = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsOtc = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSop = table.Column<bool>(type: "INTEGER", nullable: false),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pharmacy_product_facts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pharmacy_product_facts_canonical_products_CanonicalProductId",
                        column: x => x.CanonicalProductId,
                        principalTable: "canonical_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pharmacy_locations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    City = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Province = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    PostalCode = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Latitude = table.Column<decimal>(type: "TEXT", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "TEXT", precision: 9, scale: 6, nullable: true),
                    OpeningHoursJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pharmacy_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pharmacy_locations_sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "source_products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CanonicalProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    SourceProductKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Brand = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Sku = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Gtin = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    AvailabilityText = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    LastScrapedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSuccessAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_source_products_canonical_products_CanonicalProductId",
                        column: x => x.CanonicalProductId,
                        principalTable: "canonical_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_source_products_sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "source_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    FinishedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ItemsFound = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemsSaved = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorText = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_source_runs_sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alert_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CanonicalProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetPrice = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    NotifyOnRestock = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alert_rules_canonical_products_CanonicalProductId",
                        column: x => x.CanonicalProductId,
                        principalTable: "canonical_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_alert_rules_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "click_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CanonicalProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OfferUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    UtmSource = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    UtmCampaign = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ClickedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_click_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_click_events_canonical_products_CanonicalProductId",
                        column: x => x.CanonicalProductId,
                        principalTable: "canonical_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_click_events_sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_click_events_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "company_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CompanyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_company_members_companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_company_members_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pharmacy_reservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CanonicalProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ReservationType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    NreCode = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pharmacy_reservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pharmacy_reservations_canonical_products_CanonicalProductId",
                        column: x => x.CanonicalProductId,
                        principalTable: "canonical_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pharmacy_reservations_sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pharmacy_reservations_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "wishlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CanonicalProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wishlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wishlists_canonical_products_CanonicalProductId",
                        column: x => x.CanonicalProductId,
                        principalTable: "canonical_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wishlists_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "price_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    AvailabilityText = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_price_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_price_history_source_products_SourceProductId",
                        column: x => x.SourceProductId,
                        principalTable: "source_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    PriceText = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    AvailabilityText = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    StockStatus = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ShippingText = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    OfferUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ScrapedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IsLatest = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_offers_source_products_SourceProductId",
                        column: x => x.SourceProductId,
                        principalTable: "source_products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "source_run_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Level = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_run_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_source_run_logs_source_runs_SourceRunId",
                        column: x => x.SourceRunId,
                        principalTable: "source_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "alert_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AlertRuleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TriggerReason = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alert_deliveries_alert_rules_AlertRuleId",
                        column: x => x.AlertRuleId,
                        principalTable: "alert_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversion_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClickEventId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExternalOrderRef = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    CommissionAmount = table.Column<decimal>(type: "TEXT", precision: 12, scale: 2, nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: true),
                    ConvertedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversion_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conversion_events_click_events_ClickEventId",
                        column: x => x.ClickEventId,
                        principalTable: "click_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_conversion_events_sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_deliveries_AlertRuleId",
                table: "alert_deliveries",
                column: "AlertRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_rules_CanonicalProductId",
                table: "alert_rules",
                column: "CanonicalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_rules_UserId_CanonicalProductId_IsActive",
                table: "alert_rules",
                columns: new[] { "UserId", "CanonicalProductId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_canonical_product_attributes_CanonicalProductId_AttributeName",
                table: "canonical_product_attributes",
                columns: new[] { "CanonicalProductId", "AttributeName" });

            migrationBuilder.CreateIndex(
                name: "IX_canonical_products_Gtin",
                table: "canonical_products",
                column: "Gtin");

            migrationBuilder.CreateIndex(
                name: "IX_canonical_products_Slug",
                table: "canonical_products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_click_events_CanonicalProductId",
                table: "click_events",
                column: "CanonicalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_click_events_SourceId_ClickedAtUtc",
                table: "click_events",
                columns: new[] { "SourceId", "ClickedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_click_events_UserId",
                table: "click_events",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_company_members_CompanyId_UserId",
                table: "company_members",
                columns: new[] { "CompanyId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_company_members_UserId",
                table: "company_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_conversion_events_ClickEventId",
                table: "conversion_events",
                column: "ClickEventId");

            migrationBuilder.CreateIndex(
                name: "IX_conversion_events_SourceId",
                table: "conversion_events",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_pharmacy_locations_SourceId",
                table: "pharmacy_locations",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_pharmacy_product_facts_CanonicalProductId",
                table: "pharmacy_product_facts",
                column: "CanonicalProductId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pharmacy_reservations_CanonicalProductId",
                table: "pharmacy_reservations",
                column: "CanonicalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_pharmacy_reservations_SourceId",
                table: "pharmacy_reservations",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_pharmacy_reservations_UserId_CreatedAtUtc",
                table: "pharmacy_reservations",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_price_history_SourceProductId_RecordedAtUtc",
                table: "price_history",
                columns: new[] { "SourceProductId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_product_offers_SourceProductId_IsLatest",
                table: "product_offers",
                columns: new[] { "SourceProductId", "IsLatest" });

            migrationBuilder.CreateIndex(
                name: "IX_source_products_CanonicalProductId",
                table: "source_products",
                column: "CanonicalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_source_products_SourceId_SourceUrl",
                table: "source_products",
                columns: new[] { "SourceId", "SourceUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_source_run_logs_SourceRunId_CreatedAtUtc",
                table: "source_run_logs",
                columns: new[] { "SourceRunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_source_runs_SourceId_StartedAtUtc",
                table: "source_runs",
                columns: new[] { "SourceId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_sources_RetailerCode",
                table: "sources",
                column: "RetailerCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                table: "users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wishlists_CanonicalProductId",
                table: "wishlists",
                column: "CanonicalProductId");

            migrationBuilder.CreateIndex(
                name: "IX_wishlists_UserId_CanonicalProductId",
                table: "wishlists",
                columns: new[] { "UserId", "CanonicalProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_deliveries");

            migrationBuilder.DropTable(
                name: "canonical_product_attributes");

            migrationBuilder.DropTable(
                name: "company_members");

            migrationBuilder.DropTable(
                name: "conversion_events");

            migrationBuilder.DropTable(
                name: "pharmacy_locations");

            migrationBuilder.DropTable(
                name: "pharmacy_product_facts");

            migrationBuilder.DropTable(
                name: "pharmacy_reservations");

            migrationBuilder.DropTable(
                name: "price_history");

            migrationBuilder.DropTable(
                name: "product_offers");

            migrationBuilder.DropTable(
                name: "source_run_logs");

            migrationBuilder.DropTable(
                name: "wishlists");

            migrationBuilder.DropTable(
                name: "alert_rules");

            migrationBuilder.DropTable(
                name: "companies");

            migrationBuilder.DropTable(
                name: "click_events");

            migrationBuilder.DropTable(
                name: "source_products");

            migrationBuilder.DropTable(
                name: "source_runs");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "canonical_products");

            migrationBuilder.DropTable(
                name: "sources");
        }
    }
}
