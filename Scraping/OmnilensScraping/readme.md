# OmnilensScraping

Backend ASP.NET Core per discovery catalogo e scraping prodotto multi-retailer.

Retailer gestiti
- `Unieuro`
- `MediaWorld`
- `Euronics`
- `AmazonIt`

Architettura attiva
- `ScrapingController`: espone gli endpoint REST.
- `ScrapingCoordinator`: orchestration dello scraping del singolo prodotto.
- `CatalogDiscoveryService`: orchestration della discovery catalogo.
- `IRetailerScraper` + `RetailerScraperBase`: strategy per il parsing retailer-specifico.
- `ICatalogUrlSource`: strategy per l'origine degli URL catalogo.
- `RetailerRegistry`: registry dei retailer supportati e delle capability.
- `AmazonCatalogBootstrapService`: bootstrap automatico della prima snapshot Amazon IT.

Design pattern usati
- `Strategy`: `IRetailerScraper` e `ICatalogUrlSource`.
- `Registry`: `RetailerRegistry`.
- `Template Method / Inheritance`: `RetailerScraperBase`.

Principi OOP applicati
- `Incapsulamento`: controller e servizi espongono contratti stabili.
- `Ereditarieta`: gli scraper retailer riusano una base comune.
- `Polimorfismo`: scraper e catalog source vengono risolti dinamicamente in base al retailer.

Endpoint principali
- `GET /api/scraping/health`
- `GET /api/scraping/retailers`
- `GET /api/scraping/samples/{retailer}`
- `GET /api/scraping/catalog/{retailer}/count`
- `GET /api/scraping/catalog/{retailer}/parsed`
- `GET /api/scraping/catalog/{retailer}/concurrency`
- `POST /api/scraping/catalog/{retailer}/bootstrap`
- `GET /api/scraping/product`
- `POST /api/scraping/product`

Amazon IT
- Non esiste una sitemap pubblica completa.
- Il backend puo bootstrapparne una snapshot locale a partire dalle pagine pubbliche `bestsellers`.
- La snapshot viene poi riusata dagli endpoint catalogo.
