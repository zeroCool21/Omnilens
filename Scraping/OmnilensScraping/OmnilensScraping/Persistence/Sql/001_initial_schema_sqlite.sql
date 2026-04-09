PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS users (
    Id TEXT PRIMARY KEY NOT NULL,
    Email TEXT NOT NULL,
    PasswordHash TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    UserType TEXT NOT NULL,
    IsActive INTEGER NOT NULL,
    CountryCode TEXT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_users_Email ON users (Email);

CREATE TABLE IF NOT EXISTS companies (
    Id TEXT PRIMARY KEY NOT NULL,
    Name TEXT NOT NULL,
    VatCode TEXT NULL,
    CreatedAtUtc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS company_members (
    Id TEXT PRIMARY KEY NOT NULL,
    CompanyId TEXT NOT NULL,
    UserId TEXT NOT NULL,
    Role TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    CONSTRAINT FK_company_members_companies_CompanyId FOREIGN KEY (CompanyId) REFERENCES companies (Id) ON DELETE CASCADE,
    CONSTRAINT FK_company_members_users_UserId FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_company_members_CompanyId_UserId ON company_members (CompanyId, UserId);

CREATE TABLE IF NOT EXISTS sources (
    Id TEXT PRIMARY KEY NOT NULL,
    RetailerCode TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    Category TEXT NOT NULL,
    CountryCode TEXT NOT NULL,
    BaseUrl TEXT NOT NULL,
    SupportsCatalogBootstrap INTEGER NOT NULL,
    SupportsLiveScrape INTEGER NOT NULL,
    IsEnabled INTEGER NOT NULL,
    PriorityScore INTEGER NOT NULL,
    CreatedAtUtc TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_sources_RetailerCode ON sources (RetailerCode);

CREATE TABLE IF NOT EXISTS canonical_products (
    Id TEXT PRIMARY KEY NOT NULL,
    Slug TEXT NOT NULL,
    Title TEXT NOT NULL,
    Brand TEXT NULL,
    CategoryName TEXT NOT NULL,
    Gtin TEXT NULL,
    CanonicalSku TEXT NULL,
    ImageUrl TEXT NULL,
    Description TEXT NULL,
    Vertical TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_canonical_products_Slug ON canonical_products (Slug);
CREATE INDEX IF NOT EXISTS IX_canonical_products_Gtin ON canonical_products (Gtin);

CREATE TABLE IF NOT EXISTS canonical_product_attributes (
    Id TEXT PRIMARY KEY NOT NULL,
    CanonicalProductId TEXT NOT NULL,
    AttributeName TEXT NOT NULL,
    AttributeValue TEXT NOT NULL,
    CONSTRAINT FK_canonical_product_attributes_canonical_products_CanonicalProductId FOREIGN KEY (CanonicalProductId) REFERENCES canonical_products (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_canonical_product_attributes_CanonicalProductId_AttributeName ON canonical_product_attributes (CanonicalProductId, AttributeName);

CREATE TABLE IF NOT EXISTS source_products (
    Id TEXT PRIMARY KEY NOT NULL,
    SourceId TEXT NOT NULL,
    CanonicalProductId TEXT NULL,
    SourceUrl TEXT NOT NULL,
    SourceProductKey TEXT NULL,
    Title TEXT NOT NULL,
    Brand TEXT NULL,
    Sku TEXT NULL,
    Gtin TEXT NULL,
    Currency TEXT NULL,
    AvailabilityText TEXT NULL,
    ImageUrl TEXT NULL,
    Description TEXT NULL,
    LastScrapedAtUtc TEXT NOT NULL,
    LastSuccessAtUtc TEXT NULL,
    IsActive INTEGER NOT NULL,
    CONSTRAINT FK_source_products_sources_SourceId FOREIGN KEY (SourceId) REFERENCES sources (Id) ON DELETE CASCADE,
    CONSTRAINT FK_source_products_canonical_products_CanonicalProductId FOREIGN KEY (CanonicalProductId) REFERENCES canonical_products (Id) ON DELETE SET NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_source_products_SourceId_SourceUrl ON source_products (SourceId, SourceUrl);
CREATE INDEX IF NOT EXISTS IX_source_products_CanonicalProductId ON source_products (CanonicalProductId);

CREATE TABLE IF NOT EXISTS product_offers (
    Id TEXT PRIMARY KEY NOT NULL,
    SourceProductId TEXT NOT NULL,
    Price NUMERIC NULL,
    PriceText TEXT NULL,
    Currency TEXT NULL,
    AvailabilityText TEXT NULL,
    StockStatus TEXT NULL,
    ShippingText TEXT NULL,
    OfferUrl TEXT NOT NULL,
    ScrapedAtUtc TEXT NOT NULL,
    IsLatest INTEGER NOT NULL,
    CONSTRAINT FK_product_offers_source_products_SourceProductId FOREIGN KEY (SourceProductId) REFERENCES source_products (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_product_offers_SourceProductId_IsLatest ON product_offers (SourceProductId, IsLatest);

CREATE TABLE IF NOT EXISTS price_history (
    Id TEXT PRIMARY KEY NOT NULL,
    SourceProductId TEXT NOT NULL,
    Price NUMERIC NULL,
    Currency TEXT NULL,
    AvailabilityText TEXT NULL,
    RecordedAtUtc TEXT NOT NULL,
    CONSTRAINT FK_price_history_source_products_SourceProductId FOREIGN KEY (SourceProductId) REFERENCES source_products (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_price_history_SourceProductId_RecordedAtUtc ON price_history (SourceProductId, RecordedAtUtc);

CREATE TABLE IF NOT EXISTS wishlists (
    Id TEXT PRIMARY KEY NOT NULL,
    UserId TEXT NOT NULL,
    CanonicalProductId TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    CONSTRAINT FK_wishlists_users_UserId FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE,
    CONSTRAINT FK_wishlists_canonical_products_CanonicalProductId FOREIGN KEY (CanonicalProductId) REFERENCES canonical_products (Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_wishlists_UserId_CanonicalProductId ON wishlists (UserId, CanonicalProductId);

CREATE TABLE IF NOT EXISTS alert_rules (
    Id TEXT PRIMARY KEY NOT NULL,
    UserId TEXT NOT NULL,
    CanonicalProductId TEXT NOT NULL,
    TargetPrice NUMERIC NULL,
    NotifyOnRestock INTEGER NOT NULL,
    IsActive INTEGER NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    CONSTRAINT FK_alert_rules_users_UserId FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE,
    CONSTRAINT FK_alert_rules_canonical_products_CanonicalProductId FOREIGN KEY (CanonicalProductId) REFERENCES canonical_products (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_alert_rules_UserId_CanonicalProductId_IsActive ON alert_rules (UserId, CanonicalProductId, IsActive);

CREATE TABLE IF NOT EXISTS alert_deliveries (
    Id TEXT PRIMARY KEY NOT NULL,
    AlertRuleId TEXT NOT NULL,
    TriggerReason TEXT NOT NULL,
    PayloadJson TEXT NOT NULL,
    DeliveredAtUtc TEXT NOT NULL,
    CONSTRAINT FK_alert_deliveries_alert_rules_AlertRuleId FOREIGN KEY (AlertRuleId) REFERENCES alert_rules (Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS click_events (
    Id TEXT PRIMARY KEY NOT NULL,
    UserId TEXT NULL,
    CanonicalProductId TEXT NOT NULL,
    SourceId TEXT NOT NULL,
    OfferUrl TEXT NOT NULL,
    UtmSource TEXT NULL,
    UtmCampaign TEXT NULL,
    ClickedAtUtc TEXT NOT NULL,
    CONSTRAINT FK_click_events_users_UserId FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE SET NULL,
    CONSTRAINT FK_click_events_canonical_products_CanonicalProductId FOREIGN KEY (CanonicalProductId) REFERENCES canonical_products (Id) ON DELETE CASCADE,
    CONSTRAINT FK_click_events_sources_SourceId FOREIGN KEY (SourceId) REFERENCES sources (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_click_events_SourceId_ClickedAtUtc ON click_events (SourceId, ClickedAtUtc);

CREATE TABLE IF NOT EXISTS conversion_events (
    Id TEXT PRIMARY KEY NOT NULL,
    ClickEventId TEXT NULL,
    SourceId TEXT NOT NULL,
    ExternalOrderRef TEXT NULL,
    CommissionAmount NUMERIC NULL,
    Currency TEXT NULL,
    ConvertedAtUtc TEXT NOT NULL,
    CONSTRAINT FK_conversion_events_click_events_ClickEventId FOREIGN KEY (ClickEventId) REFERENCES click_events (Id) ON DELETE SET NULL,
    CONSTRAINT FK_conversion_events_sources_SourceId FOREIGN KEY (SourceId) REFERENCES sources (Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS source_runs (
    Id TEXT PRIMARY KEY NOT NULL,
    SourceId TEXT NOT NULL,
    RunType TEXT NOT NULL,
    Status TEXT NOT NULL,
    StartedAtUtc TEXT NOT NULL,
    FinishedAtUtc TEXT NULL,
    ItemsFound INTEGER NOT NULL,
    ItemsSaved INTEGER NOT NULL,
    ErrorText TEXT NULL,
    CONSTRAINT FK_source_runs_sources_SourceId FOREIGN KEY (SourceId) REFERENCES sources (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_source_runs_SourceId_StartedAtUtc ON source_runs (SourceId, StartedAtUtc);

CREATE TABLE IF NOT EXISTS source_run_logs (
    Id TEXT PRIMARY KEY NOT NULL,
    SourceRunId TEXT NOT NULL,
    Level TEXT NOT NULL,
    Message TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    CONSTRAINT FK_source_run_logs_source_runs_SourceRunId FOREIGN KEY (SourceRunId) REFERENCES source_runs (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_source_run_logs_SourceRunId_CreatedAtUtc ON source_run_logs (SourceRunId, CreatedAtUtc);

CREATE TABLE IF NOT EXISTS pharmacy_product_facts (
    Id TEXT PRIMARY KEY NOT NULL,
    CanonicalProductId TEXT NOT NULL,
    ActiveIngredient TEXT NULL,
    DosageForm TEXT NULL,
    StrengthText TEXT NULL,
    PackageSize TEXT NULL,
    RequiresPrescription INTEGER NOT NULL,
    IsOtc INTEGER NOT NULL,
    IsSop INTEGER NOT NULL,
    Manufacturer TEXT NULL,
    CONSTRAINT FK_pharmacy_product_facts_canonical_products_CanonicalProductId FOREIGN KEY (CanonicalProductId) REFERENCES canonical_products (Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS IX_pharmacy_product_facts_CanonicalProductId ON pharmacy_product_facts (CanonicalProductId);

CREATE TABLE IF NOT EXISTS pharmacy_locations (
    Id TEXT PRIMARY KEY NOT NULL,
    SourceId TEXT NOT NULL,
    Name TEXT NOT NULL,
    Address TEXT NOT NULL,
    City TEXT NOT NULL,
    Province TEXT NULL,
    PostalCode TEXT NULL,
    Latitude NUMERIC NULL,
    Longitude NUMERIC NULL,
    OpeningHoursJson TEXT NULL,
    CONSTRAINT FK_pharmacy_locations_sources_SourceId FOREIGN KEY (SourceId) REFERENCES sources (Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS pharmacy_reservations (
    Id TEXT PRIMARY KEY NOT NULL,
    UserId TEXT NOT NULL,
    SourceId TEXT NOT NULL,
    CanonicalProductId TEXT NOT NULL,
    ReservationType TEXT NOT NULL,
    NreCode TEXT NULL,
    Status TEXT NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    CONSTRAINT FK_pharmacy_reservations_users_UserId FOREIGN KEY (UserId) REFERENCES users (Id) ON DELETE CASCADE,
    CONSTRAINT FK_pharmacy_reservations_sources_SourceId FOREIGN KEY (SourceId) REFERENCES sources (Id) ON DELETE CASCADE,
    CONSTRAINT FK_pharmacy_reservations_canonical_products_CanonicalProductId FOREIGN KEY (CanonicalProductId) REFERENCES canonical_products (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_pharmacy_reservations_UserId_CreatedAtUtc ON pharmacy_reservations (UserId, CreatedAtUtc);
