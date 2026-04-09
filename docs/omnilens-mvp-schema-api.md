# OmniLens+ - MVP Schema DB e API

## Obiettivo

Definire una base concreta per implementare l'MVP di OmniLens+ partendo dal codice gia presente nel repo.

Questo documento risponde a 4 domande:

1. Cosa esiste gia nel progetto.
2. Cosa deve essere aggiunto per arrivare all'MVP.
3. Quale schema dati usare.
4. Quali endpoint API esporre per backend e frontend.

---

## 1. Stato attuale del repo

Nel repo esiste gia un backend ASP.NET Core focalizzato su scraping e catalog discovery:

- progetto attuale: `Scraping/OmnilensScraping`
- controller principale: `ScrapingController`
- modello prodotto gia normalizzato: `ProductData`
- supporto retailer gia presente, inclusi retailer `Pharmacy`

Quindi non conviene ripartire da zero.

La scelta migliore per l'MVP e:

- riusare `OmnilensScraping` come base del motore di acquisizione;
- aggiungere un persistence layer vero;
- aggiungere una query API orientata a catalogo/ricerca;
- aggiungere identity, wishlist, alert e tracking.

---

## 2. Boundary consigliato per l'MVP

### Dentro l'MVP

- account utente e profilo base;
- account azienda base;
- ricerca prodotti;
- comparazione prezzi multi-retailer;
- storico prezzi;
- wishlist;
- alert su prezzo/disponibilita;
- dashboard B2C minima;
- dashboard B2B minima;
- tracking referral/click;
- un verticale iniziale reale:
  - farmacia se vuoi restare sul dominio originale;
  - elettronica se vuoi sfruttare subito gli scraper gia pronti.

### Fuori dall'MVP

- social listening avanzato;
- AI predittiva avanzata;
- wallet e BNPL;
- community;
- telemedicina profonda;
- ecosistema multi-prodotto completo;
- microservizi separati.

Per l'MVP basta un `modular monolith` ben separato per domini.

---

## 3. Architettura MVP consigliata

## 3.1 Applicazione

Un solo backend ASP.NET Core con moduli interni:

- `Identity`
- `CatalogIngestion`
- `CatalogQuery`
- `UserTracking`
- `Alerts`
- `Admin`

## 3.2 Moduli tecnici

- `Controllers`
- `Application`
- `Domain`
- `Infrastructure`
- `Persistence`
- `BackgroundJobs`

## 3.3 Cosa riusare subito dal progetto attuale

- `ScrapingCoordinator`
- `CatalogDiscoveryService`
- `ParallelScrapingService`
- `RetailerRegistry`
- scraper retailer-specifici
- `ProductData`

## 3.4 Cosa aggiungere

- Entity Framework Core
- PostgreSQL
- autenticazione JWT/cookie
- background jobs per refresh catalogo
- persistence per snapshot prezzi
- query API per frontend

---

## 4. Modello dati concettuale

Il problema reale dell'MVP non e "scrapare una pagina".
Il problema reale e trasformare pagine diverse in:

- prodotto canonico;
- offerte multiple per retailer;
- storico prezzi;
- preferenze utente;
- alert.

Per questo servono 3 livelli:

1. `Source/Retailer`
2. `SourceProduct/Offer`
3. `CanonicalProduct`

---

## 5. Schema DB minimo consigliato

## 5.1 Identity

### Table `users`

- `id` UUID PK
- `email` varchar unique not null
- `password_hash` varchar not null
- `display_name` varchar not null
- `user_type` varchar not null
- `is_active` boolean not null
- `country_code` varchar(2) null
- `created_at_utc` timestamptz not null
- `updated_at_utc` timestamptz not null

### Table `companies`

- `id` UUID PK
- `name` varchar not null
- `vat_code` varchar null
- `created_at_utc` timestamptz not null

### Table `company_members`

- `id` UUID PK
- `company_id` UUID FK -> companies.id
- `user_id` UUID FK -> users.id
- `role` varchar not null
- `created_at_utc` timestamptz not null

---

## 5.2 Source registry

### Table `sources`

- `id` UUID PK
- `retailer_code` varchar unique not null
- `display_name` varchar not null
- `category` varchar not null
- `country_code` varchar(2) not null
- `base_url` varchar not null
- `supports_catalog_bootstrap` boolean not null
- `supports_live_scrape` boolean not null
- `is_enabled` boolean not null
- `priority_score` int not null
- `created_at_utc` timestamptz not null

Questa tabella rappresenta il registry persistito dei retailer.

---

## 5.3 Catalogo canonico

### Table `canonical_products`

- `id` UUID PK
- `slug` varchar unique not null
- `title` varchar not null
- `brand` varchar null
- `category_name` varchar not null
- `gtin` varchar null
- `canonical_sku` varchar null
- `image_url` varchar null
- `description` text null
- `vertical` varchar not null
- `created_at_utc` timestamptz not null
- `updated_at_utc` timestamptz not null

### Table `canonical_product_attributes`

- `id` UUID PK
- `canonical_product_id` UUID FK -> canonical_products.id
- `attribute_name` varchar not null
- `attribute_value` text not null

Serve per attributi dinamici: taglia, colore, principio attivo, capacita, ecc.

---

## 5.4 Prodotti di sorgente

### Table `source_products`

- `id` UUID PK
- `source_id` UUID FK -> sources.id
- `canonical_product_id` UUID FK -> canonical_products.id nullable
- `source_url` varchar not null
- `source_product_key` varchar null
- `title` varchar not null
- `brand` varchar null
- `sku` varchar null
- `gtin` varchar null
- `currency` varchar(3) null
- `availability_text` varchar null
- `image_url` varchar null
- `description` text null
- `last_scraped_at_utc` timestamptz not null
- `last_success_at_utc` timestamptz null
- `is_active` boolean not null

Questa tabella e l'entita bridge tra scraper e catalogo.

---

## 5.5 Offerte correnti

### Table `product_offers`

- `id` UUID PK
- `source_product_id` UUID FK -> source_products.id
- `price` numeric(12,2) null
- `price_text` varchar null
- `currency` varchar(3) null
- `availability_text` varchar null
- `stock_status` varchar null
- `shipping_text` varchar null
- `offer_url` varchar not null
- `scraped_at_utc` timestamptz not null
- `is_latest` boolean not null

Per ogni `source_product` tieni una sola offer corrente marcata `is_latest = true`.

---

## 5.6 Storico prezzi

### Table `price_history`

- `id` UUID PK
- `source_product_id` UUID FK -> source_products.id
- `price` numeric(12,2) null
- `currency` varchar(3) null
- `availability_text` varchar null
- `recorded_at_utc` timestamptz not null

Indice consigliato:

- `(source_product_id, recorded_at_utc desc)`

---

## 5.7 Wishlist e alert

### Table `wishlists`

- `id` UUID PK
- `user_id` UUID FK -> users.id
- `canonical_product_id` UUID FK -> canonical_products.id
- `created_at_utc` timestamptz not null

Vincolo unico consigliato:

- `(user_id, canonical_product_id)`

### Table `alert_rules`

- `id` UUID PK
- `user_id` UUID FK -> users.id
- `canonical_product_id` UUID FK -> canonical_products.id
- `target_price` numeric(12,2) null
- `notify_on_restock` boolean not null
- `is_active` boolean not null
- `created_at_utc` timestamptz not null

### Table `alert_deliveries`

- `id` UUID PK
- `alert_rule_id` UUID FK -> alert_rules.id
- `trigger_reason` varchar not null
- `payload_json` text not null
- `delivered_at_utc` timestamptz not null

---

## 5.8 Tracking referral

### Table `click_events`

- `id` UUID PK
- `user_id` UUID FK -> users.id nullable
- `canonical_product_id` UUID FK -> canonical_products.id
- `source_id` UUID FK -> sources.id
- `offer_url` varchar not null
- `utm_source` varchar null
- `utm_campaign` varchar null
- `clicked_at_utc` timestamptz not null

### Table `conversion_events`

- `id` UUID PK
- `click_event_id` UUID FK -> click_events.id nullable
- `source_id` UUID FK -> sources.id
- `external_order_ref` varchar null
- `commission_amount` numeric(12,2) null
- `currency` varchar(3) null
- `converted_at_utc` timestamptz not null

---

## 5.9 Admin e ingestion health

### Table `source_runs`

- `id` UUID PK
- `source_id` UUID FK -> sources.id
- `run_type` varchar not null
- `status` varchar not null
- `started_at_utc` timestamptz not null
- `finished_at_utc` timestamptz null
- `items_found` int not null
- `items_saved` int not null
- `error_text` text null

### Table `source_run_logs`

- `id` UUID PK
- `source_run_id` UUID FK -> source_runs.id
- `level` varchar not null
- `message` text not null
- `created_at_utc` timestamptz not null

---

## 6. Verticale farmacia: estensioni schema

Se l'MVP parte da farmacia, aggiungi solo questo extra:

### Table `pharmacy_product_facts`

- `id` UUID PK
- `canonical_product_id` UUID FK -> canonical_products.id
- `active_ingredient` varchar null
- `dosage_form` varchar null
- `strength_text` varchar null
- `package_size` varchar null
- `requires_prescription` boolean not null
- `is_otc` boolean not null
- `is_sop` boolean not null
- `manufacturer` varchar null

### Table `pharmacy_locations`

- `id` UUID PK
- `source_id` UUID FK -> sources.id
- `name` varchar not null
- `address` varchar not null
- `city` varchar not null
- `province` varchar null
- `postal_code` varchar null
- `latitude` numeric(9,6) null
- `longitude` numeric(9,6) null
- `opening_hours_json` text null

### Table `pharmacy_reservations`

- `id` UUID PK
- `user_id` UUID FK -> users.id
- `source_id` UUID FK -> sources.id
- `canonical_product_id` UUID FK -> canonical_products.id
- `reservation_type` varchar not null
- `nre_code` varchar null
- `status` varchar not null
- `created_at_utc` timestamptz not null

Nota:

- niente vendita diretta di farmaci con prescrizione nell'MVP;
- solo prenotazione/ritiro o workflow consentito dal partner.

---

## 7. API MVP consigliata

L'API attuale di scraping puo restare, ma va affiancata da una `query API` per il prodotto vero.

## 7.1 Area Auth

### `POST /api/auth/register`

Request:

```json
{
  "email": "user@example.com",
  "password": "secret",
  "displayName": "Mario",
  "userType": "B2C"
}
```

### `POST /api/auth/login`

### `POST /api/auth/refresh`

### `POST /api/auth/logout`

### `GET /api/auth/me`

---

## 7.2 Area Catalog Query

### `GET /api/products/search`

Query params:

- `q`
- `category`
- `brand`
- `minPrice`
- `maxPrice`
- `availability`
- `source`
- `page`
- `pageSize`
- `sort`

Response:

```json
{
  "items": [
    {
      "id": "uuid",
      "title": "Product name",
      "brand": "Brand",
      "category": "Electronics",
      "imageUrl": "https://...",
      "bestPrice": 199.99,
      "currency": "EUR",
      "sourceCount": 3,
      "isWishlisted": true
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 120
}
```

### `GET /api/products/{id}`

Dettaglio prodotto canonico.

### `GET /api/products/{id}/offers`

Elenco offerte correnti per retailer.

### `GET /api/products/{id}/price-history`

Storico prezzi aggregato o per retailer.

### `GET /api/products/{id}/related`

Alternative o equivalenti economici.

---

## 7.3 Area Wishlist e alert

### `GET /api/me/wishlist`

### `POST /api/me/wishlist`

```json
{
  "canonicalProductId": "uuid"
}
```

### `DELETE /api/me/wishlist/{canonicalProductId}`

### `GET /api/me/alerts`

### `POST /api/me/alerts`

```json
{
  "canonicalProductId": "uuid",
  "targetPrice": 149.99,
  "notifyOnRestock": true
}
```

### `PATCH /api/me/alerts/{id}`

### `DELETE /api/me/alerts/{id}`

---

## 7.4 Area Dashboard B2C

### `GET /api/me/dashboard`

Dati aggregati per:

- wishlist
- ultimi alert
- prodotti monitorati
- offerte recenti

---

## 7.5 Area B2B

### `GET /api/b2b/dashboard`

### `GET /api/b2b/products/benchmark`

### `GET /api/b2b/products/{id}/history`

### `GET /api/b2b/reports/export`

### `POST /api/b2b/team/invite`

### `GET /api/b2b/team/members`

Per l'MVP basta benchmark semplice, non forecasting avanzato.

---

## 7.6 Area Tracking

### `POST /api/tracking/click`

```json
{
  "canonicalProductId": "uuid",
  "sourceId": "uuid",
  "offerUrl": "https://..."
}
```

### `POST /api/tracking/conversion`

Usabile solo se hai una fonte affidabile di conversione.

---

## 7.7 Area ingestion/admin

L'API attuale di scraping resta valida:

- `GET /api/scraping/health`
- `GET /api/scraping/retailers`
- `GET /api/scraping/catalog/{retailer}/count`
- `GET /api/scraping/catalog/{retailer}/parsed`
- `POST /api/scraping/catalog/{retailer}/bootstrap`
- `GET /api/scraping/product`
- `POST /api/scraping/product`

Da aggiungere:

### `POST /api/admin/sources/{sourceId}/refresh`

Lancia un refresh manuale.

### `GET /api/admin/sources`

### `GET /api/admin/sources/{sourceId}/runs`

### `GET /api/admin/sources/{sourceId}/health`

### `POST /api/admin/products/reconcile`

Per riconciliare manualmente prodotti non agganciati al canonico.

---

## 8. Query principali da supportare

Le query che contano davvero nell'MVP sono queste:

1. Cerca prodotto per keyword.
2. Recupera miglior prezzo per prodotto.
3. Recupera tutte le offerte correnti per prodotto.
4. Recupera lo storico prezzi di una offerta.
5. Recupera wishlist utente.
6. Recupera alert attivi dell'utente.
7. Recupera benchmark semplice per B2B.

---

## 9. Background jobs necessari

## Job J01 - Catalog refresh

- scorre le fonti abilitate;
- aggiorna prodotti e offerte;
- scrive `source_runs`.

## Job J02 - Price history compaction

- evita duplicati inutili;
- salva solo i cambi significativi.

## Job J03 - Alert evaluation

- confronta prezzo corrente con regole attive;
- invia alert.

## Job J04 - Product reconciliation

- propone match automatici per titolo/brand/gtin;
- lascia casi dubbi in review.

---

## 10. Mapping tra codice attuale e target MVP

## Gia presente

- scraper multi-retailer
- controller scraping
- modello `ProductData`
- batch/concurrency benchmark
- catalog bootstrap

## Da introdurre subito

- `DbContext`
- persistence entities
- migration iniziale
- moduli auth
- moduli query API
- moduli wishlist/alert
- pannello admin minimo

## Da rinominare o separare logicamente

- `ProductData` puo restare come DTO di ingestion
- servono pero entita persistenti separate:
  - `CanonicalProduct`
  - `SourceProduct`
  - `ProductOffer`
  - `PriceHistory`

---

## 11. Sequenza implementativa consigliata

### Step 1

- aggiungere EF Core + PostgreSQL
- creare migration iniziale
- persistere `sources`, `source_products`, `product_offers`, `price_history`

### Step 2

- creare riconciliazione minima verso `canonical_products`
- esporre `GET /api/products/search`
- esporre `GET /api/products/{id}`
- esporre `GET /api/products/{id}/offers`

### Step 3

- aggiungere auth
- aggiungere wishlist
- aggiungere alert
- aggiungere dashboard personale

### Step 4

- aggiungere dashboard B2B minima
- aggiungere benchmark/export base
- aggiungere tracking click/referral

### Step 5

- se verticale farmacia:
  - aggiungere `pharmacy_product_facts`
  - aggiungere `pharmacy_locations`
  - aggiungere prenotazioni/ritiro

---

## 12. Decisioni consigliate subito

Ti conviene fissare subito queste scelte:

1. `PostgreSQL` come DB principale MVP.
2. `EF Core` come ORM.
3. `modular monolith` e non microservizi per MVP.
4. `OmnilensScraping` come modulo ingestion del backend.
5. verticale iniziale unico:
   - `Electronics` se vuoi velocita di esecuzione;
   - `Pharmacy` se vuoi allineamento al concept originale.

---

## 13. Output atteso dell'MVP

Quando questo blueprint e implementato, il prodotto deve gia poter:

- acquisire dati da piu retailer;
- persistere offerte e storico prezzi;
- cercare prodotti e confrontare retailer;
- far salvare prodotti preferiti;
- inviare alert;
- mostrare una vista B2C e una B2B;
- supportare un verticale reale pronto da testare con utenti.

