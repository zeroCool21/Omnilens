# OmniLens+ - Backlog Tecnico

## Scopo

Questo documento traduce la roadmap funzionale in un backlog tecnico operativo, organizzato come:

- `Epic`: macro-area di sviluppo.
- `Feature`: blocco rilasciabile.
- `Task`: unita tecnica implementabile.

## Convenzioni

- `P0`: indispensabile per MVP o fondazioni.
- `P1`: importante per Fase 2.
- `P2`: espansione/Fase 3.
- `B2C`: utente finale.
- `B2B`: azienda/partner.

---

# Fase MVP

## Epic E01 - Fondazioni piattaforma (`P0`)

### Feature E01.F01 - Identity e accesso

- Task E01.F01.T01: definire modello `User`.
- Task E01.F01.T02: definire modello `Company`.
- Task E01.F01.T03: definire modello `Role` e `Permission`.
- Task E01.F01.T04: implementare registrazione utente.
- Task E01.F01.T05: implementare login.
- Task E01.F01.T06: implementare recupero password.
- Task E01.F01.T07: implementare refresh token/session management.
- Task E01.F01.T08: esporre endpoint `me/profile`.

### Feature E01.F02 - Classificazione utenza

- Task E01.F02.T01: aggiungere `user_type` (`B2C`, `B2B`, `admin`, `partner`).
- Task E01.F02.T02: impostare redirect/layout in base al tipo utente.
- Task E01.F02.T03: proteggere route e permessi lato backend.
- Task E01.F02.T04: proteggere route e permessi lato frontend.

### Feature E01.F03 - Struttura abbonamenti

- Task E01.F03.T01: definire entita `ProductFamily`.
- Task E01.F03.T02: definire entita `Plan`.
- Task E01.F03.T03: definire entita `Entitlement`.
- Task E01.F03.T04: definire entita `Subscription`.
- Task E01.F03.T05: creare pagina abbonamenti `Famiglia -> Piano -> Opzioni`.
- Task E01.F03.T06: predisporre logica per piani separati B2C/B2B.

### Feature E01.F04 - Profilo e impostazioni

- Task E01.F04.T01: creare pagina profilo utente.
- Task E01.F04.T02: aggiungere preferenze notifiche.
- Task E01.F04.T03: aggiungere gestione consensi privacy.
- Task E01.F04.T04: aggiungere preferenze lingua/paese/settore.

---

## Epic E02 - Acquisizione dati e registry sorgenti (`P0`)

### Feature E02.F01 - Source Registry

- Task E02.F01.T01: definire modello `Source`.
- Task E02.F01.T02: definire classificazione per settore, paese, canale, priorita.
- Task E02.F01.T03: aggiungere flag `api`, `scraping`, `manual`, `disabled`.
- Task E02.F01.T04: creare pannello admin sorgenti.

### Feature E02.F02 - Framework scraping/API

- Task E02.F02.T01: definire interfaccia comune `ISourceCollector`.
- Task E02.F02.T02: implementare adapter `API collector`.
- Task E02.F02.T03: implementare adapter `Scraper collector`.
- Task E02.F02.T04: implementare pipeline `fetch -> parse -> normalize -> persist`.
- Task E02.F02.T05: standardizzare timeout, retry e backoff.

### Feature E02.F03 - Scheduler e live search

- Task E02.F03.T01: creare job scheduler per raccolta periodica.
- Task E02.F03.T02: creare orchestratore live search on demand.
- Task E02.F03.T03: definire priorita tra cache, storico recente e live fetch.
- Task E02.F03.T04: introdurre code/eventi per esecuzione asincrona.

### Feature E02.F04 - Osservabilita base ingestion

- Task E02.F04.T01: loggare esecuzioni per sorgente.
- Task E02.F04.T02: loggare errori di parsing/fetch.
- Task E02.F04.T03: calcolare health score per sorgente.
- Task E02.F04.T04: creare alert amministrativi su failure ripetute.

---

## Epic E03 - Normalizzazione catalogo e storico (`P0`)

### Feature E03.F01 - Catalogo normalizzato

- Task E03.F01.T01: definire modello `CanonicalProduct`.
- Task E03.F01.T02: definire modello `SourceProduct`.
- Task E03.F01.T03: definire mapping tra prodotto sorgente e prodotto canonico.
- Task E03.F01.T04: gestire attributi comuni: nome, brand, categoria, prezzo, disponibilita.
- Task E03.F01.T05: definire pipeline di deduplicazione.

### Feature E03.F02 - Storico prezzi

- Task E03.F02.T01: definire modello `PriceSnapshot`.
- Task E03.F02.T02: salvare snapshot per prodotto/sorgente.
- Task E03.F02.T03: salvare variazioni rilevanti.
- Task E03.F02.T04: creare query per ultimo prezzo, minimo, massimo, trend.

### Feature E03.F03 - Cache e query veloci

- Task E03.F03.T01: introdurre cache risultati ricerca.
- Task E03.F03.T02: introdurre cache prodotto dettaglio.
- Task E03.F03.T03: invalidare cache su cambi prezzo/disponibilita.

---

## Epic E04 - Motore OmniLens+ B2C (`P0`)

### Feature E04.F01 - Ricerca e listing

- Task E04.F01.T01: implementare endpoint ricerca globale.
- Task E04.F01.T02: supportare query per nome, brand, categoria, keyword.
- Task E04.F01.T03: implementare filtri principali.
- Task E04.F01.T04: implementare ordinamenti per prezzo, rilevanza, disponibilita.
- Task E04.F01.T05: creare UI risultati ricerca.

### Feature E04.F02 - Confronto offerte

- Task E04.F02.T01: aggregare offerte per prodotto canonico.
- Task E04.F02.T02: evidenziare migliore offerta.
- Task E04.F02.T03: mostrare vendor, prezzo, disponibilita e link/azione.
- Task E04.F02.T04: mostrare alternative piu economiche.

### Feature E04.F03 - Scheda prodotto

- Task E04.F03.T01: creare pagina dettaglio prodotto.
- Task E04.F03.T02: mostrare storico prezzi.
- Task E04.F03.T03: mostrare trend base.
- Task E04.F03.T04: mostrare recensioni e metadati disponibili.

### Feature E04.F04 - Wishlist e monitoraggio

- Task E04.F04.T01: definire modello `Wishlist`.
- Task E04.F04.T02: aggiungere prodotto a wishlist.
- Task E04.F04.T03: rimuovere prodotto da wishlist.
- Task E04.F04.T04: creare dashboard personale con preferiti e prodotti monitorati.

---

## Epic E05 - Notifiche e tracking conversioni (`P0`)

### Feature E05.F01 - Alert di prezzo/disponibilita

- Task E05.F01.T01: definire modello `AlertRule`.
- Task E05.F01.T02: creare worker per valutazione regole alert.
- Task E05.F01.T03: inviare notifiche email.
- Task E05.F01.T04: predisporre push/in-app notification.

### Feature E05.F02 - Tracking referral

- Task E05.F02.T01: definire modello `ClickEvent`.
- Task E05.F02.T02: definire modello `ConversionEvent`.
- Task E05.F02.T03: generare link tracciati.
- Task E05.F02.T04: creare dashboard base referral.

---

## Epic E06 - UI differenziata B2C/B2B (`P0`)

### Feature E06.F01 - Shell B2C

- Task E06.F01.T01: creare homepage B2C.
- Task E06.F01.T02: creare search bar con suggerimenti.
- Task E06.F01.T03: creare area offerte/preferiti/notifiche.

### Feature E06.F02 - Shell B2B

- Task E06.F02.T01: creare dashboard B2B minimale.
- Task E06.F02.T02: aggiungere widget base prezzi/competitor.
- Task E06.F02.T03: aggiungere export CSV/PDF.

---

## Epic E07 - B2B starter (`P0`)

### Feature E07.F01 - Benchmark competitor

- Task E07.F01.T01: definire dataset comparativo per brand/vendor.
- Task E07.F01.T02: mostrare variazioni prezzo competitor.
- Task E07.F01.T03: filtrare per categoria/periodo.

### Feature E07.F02 - Team e accessi aziendali

- Task E07.F02.T01: invitare utenti in azienda.
- Task E07.F02.T02: assegnare ruoli team.
- Task E07.F02.T03: limitare accesso a report e widget.

---

## Epic E08 - Primo verticale operativo (`P0`)

### Feature E08.F01 - Template verticale generico

- Task E08.F01.T01: definire estensione settore-specifica del catalogo.
- Task E08.F01.T02: definire attributi custom per verticale.
- Task E08.F01.T03: definire filtri custom per verticale.
- Task E08.F01.T04: definire pagine list/detail verticali.

### Feature E08.F02 - Pacchetto farmacia se scelto come primo verticale

- Task E08.F02.T01: modellare categorie `OTC`, `SOP`, integratori, cosmetici, dispositivi.
- Task E08.F02.T02: aggiungere ricerca per principio attivo.
- Task E08.F02.T03: aggiungere mappa farmacie con disponibilita locale.
- Task E08.F02.T04: aggiungere scheda farmaco con informazioni base.
- Task E08.F02.T05: aggiungere flusso prenotazione e ritiro in farmacia.
- Task E08.F02.T06: aggiungere gestione NRE/ricetta per prenotazione, non vendita diretta.
- Task E08.F02.T07: aggiungere reminder terapia/riacquisto.

---

# Fase 2

## Epic E09 - Robustezza ingestion e qualita dati (`P1`)

### Feature E09.F01 - Rilevamento cambi struttura sorgente

- Task E09.F01.T01: salvare snapshot DOM o output parser.
- Task E09.F01.T02: confrontare versioni precedenti/attuali.
- Task E09.F01.T03: rilevare anomalie dati (vuoti, zero, rotture pattern).
- Task E09.F01.T04: generare alert automatici per sorgente degradata.

### Feature E09.F02 - Fallback automatici

- Task E09.F02.T01: mantenere selettori alternativi per scraper.
- Task E09.F02.T02: eseguire fallback automatico.
- Task E09.F02.T03: validare il dato ottenuto dal fallback.
- Task E09.F02.T04: marcare la sorgente come `degraded` se necessario.

### Feature E09.F03 - Prioritizzazione fonti

- Task E09.F03.T01: introdurre scoring Pareto per fonti.
- Task E09.F03.T02: concentrare frequenza alta sulle fonti ad alto rendimento.
- Task E09.F03.T03: ridurre polling su fonti secondarie.

---

## Epic E10 - OmniTrend+ e intelligence social (`P1`)

### Feature E10.F01 - Trend engine

- Task E10.F01.T01: definire modello `TrendSignal`.
- Task E10.F01.T02: aggregare segnali per paese, regione, settore.
- Task E10.F01.T03: calcolare score trend.
- Task E10.F01.T04: esporre dashboard trend.

### Feature E10.F02 - Ingestione fonti pubbliche social/news/video

- Task E10.F02.T01: definire collector per social pubblici.
- Task E10.F02.T02: definire collector per news/blog/community.
- Task E10.F02.T03: definire collector per pagine video/canali.
- Task E10.F02.T04: normalizzare testo, tag, metadati e geografia.

### Feature E10.F03 - Trascrizione e summarization

- Task E10.F03.T01: estrarre audio da video supportati.
- Task E10.F03.T02: trascrivere audio in testo.
- Task E10.F03.T03: riassumere il contenuto in base alla famiglia prodotto.
- Task E10.F03.T04: collegare insight ai trend o ai prodotti.

### Feature E10.F04 - Sentiment e brand monitoring

- Task E10.F04.T01: classificare sentiment per brand/prodotto/tema.
- Task E10.F04.T02: calcolare health score del brand.
- Task E10.F04.T03: generare alert su picchi negativi/positivi.

---

## Epic E11 - B2B analytics pro (`P1`)

### Feature E11.F01 - Forecasting e pricing

- Task E11.F01.T01: introdurre forecasting domanda.
- Task E11.F01.T02: introdurre forecasting prezzo.
- Task E11.F01.T03: introdurre suggerimenti di dynamic pricing.
- Task E11.F01.T04: calcolare margini e sensibilita prezzo.

### Feature E11.F02 - Reporting avanzato

- Task E11.F02.T01: generare report schedulati.
- Task E11.F02.T02: introdurre template report per settore.
- Task E11.F02.T03: esportare CSV, XLSX, PDF.
- Task E11.F02.T04: inviare report email a team aziendali.

### Feature E11.F03 - API dati B2B

- Task E11.F03.T01: definire API key / OAuth client.
- Task E11.F03.T02: esporre endpoint prezzi correnti.
- Task E11.F03.T03: esporre endpoint storico prezzi.
- Task E11.F03.T04: esporre endpoint trend e forecast.
- Task E11.F03.T05: introdurre rate limiting e quote per piano.

### Feature E11.F04 - Integrazioni aziendali

- Task E11.F04.T01: predisporre export verso CRM/ERP.
- Task E11.F04.T02: predisporre webhook.
- Task E11.F04.T03: predisporre dataset scaricabili.

---

## Epic E12 - Monetizzazione estesa e loyalty (`P1`)

### Feature E12.F01 - Cashback e reward

- Task E12.F01.T01: definire modello `RewardLedger`.
- Task E12.F01.T02: accreditare cashback da conversione.
- Task E12.F01.T03: mostrare saldo reward/wallet.
- Task E12.F01.T04: usare reward per sconti o accessi premium.

### Feature E12.F02 - Wallet

- Task E12.F02.T01: definire movimenti wallet.
- Task E12.F02.T02: collegare wallet a cashback e abbonamenti.
- Task E12.F02.T03: gestire storico movimenti.

### Feature E12.F03 - Fintech base

- Task E12.F03.T01: predisporre BNPL per piani compatibili.
- Task E12.F03.T02: predisporre gift card.
- Task E12.F03.T03: predisporre micro-assicurazioni/garanzie per settori idonei.

---

## Epic E13 - Community e engagement (`P1`)

### Feature E13.F01 - Gamification

- Task E13.F01.T01: definire punti, badge, livelli.
- Task E13.F01.T02: assegnare reward per azioni rilevanti.
- Task E13.F01.T03: mostrare progressi e classifiche.

### Feature E13.F02 - Community

- Task E13.F02.T01: creare forum/gruppi.
- Task E13.F02.T02: creare feed recensioni e wishlist condivise.
- Task E13.F02.T03: moderare contenuti base.

### Feature E13.F03 - Creator e contenuti

- Task E13.F03.T01: gestire creator shop.
- Task E13.F03.T02: supportare tutorial e video brevi.
- Task E13.F03.T03: supportare live shopping in forma iniziale.

---

## Epic E14 - Sanita estesa (`P1`)

### Feature E14.F01 - Farmacia avanzata

- Task E14.F01.T01: suggerire equivalenti/generici.
- Task E14.F01.T02: monitorare carenze farmaci.
- Task E14.F01.T03: integrare stock farmacie e grossisti.
- Task E14.F01.T04: gestire alert ritorno disponibilita farmaci critici.
- Task E14.F01.T05: introdurre chat con farmacista.

### Feature E14.F02 - Telemedicina base

- Task E14.F02.T01: agenda consulti.
- Task E14.F02.T02: prenotazione video consulto.
- Task E14.F02.T03: gestione documenti allegati.
- Task E14.F02.T04: second opinion base.

### Feature E14.F03 - Cartella e salute personale

- Task E14.F03.T01: definire modello cartella clinica digitale.
- Task E14.F03.T02: caricare referti ed esami.
- Task E14.F03.T03: storico risultati laboratorio.
- Task E14.F03.T04: reminder ricette e terapie.

### Feature E14.F04 - Prenotazioni salute

- Task E14.F04.T01: prenotare visite e screening.
- Task E14.F04.T02: prenotare check-up.
- Task E14.F04.T03: gestire strutture pubbliche/private in anagrafica.

---

## Epic E15 - Nuovi verticali (`P1`)

### Feature E15.F01 - OmniTravel+

- Task E15.F01.T01: modellare entita viaggio.
- Task E15.F01.T02: implementare confronto voli/hotel.
- Task E15.F01.T03: implementare storico e alert tratte.

### Feature E15.F02 - OmniGrocery+

- Task E15.F02.T01: modellare entita grocery.
- Task E15.F02.T02: implementare confronto supermercati.
- Task E15.F02.T03: implementare promemoria acquisti ricorrenti.

### Feature E15.F03 - OmniEducation+

- Task E15.F03.T01: modellare entita corso/provider.
- Task E15.F03.T02: implementare confronto corsi e certificazioni.

### Feature E15.F04 - OmniRealty+

- Task E15.F04.T01: modellare entita immobile.
- Task E15.F04.T02: implementare confronto offerte immobiliari.

### Feature E15.F05 - OmniPlay+

- Task E15.F05.T01: modellare entita gioco/console/abbonamento.
- Task E15.F05.T02: implementare confronto gaming e digital delivery.

---

# Fase 3

## Epic E16 - Ecosistema prodotti (`P2`)

### Feature E16.F01 - OmniSocial+

- Task E16.F01.T01: publishing multicanale.
- Task E16.F01.T02: calendario editoriale.
- Task E16.F01.T03: analytics performance account.

### Feature E16.F02 - OmniAds+

- Task E16.F02.T01: gestione campagne.
- Task E16.F02.T02: analisi creativita competitor.
- Task E16.F02.T03: suggerimenti AI per campagne.

### Feature E16.F03 - OmniInfluence+

- Task E16.F03.T01: database creator.
- Task E16.F03.T02: matching brand-creator.
- Task E16.F03.T03: tracking performance collaborazioni.

### Feature E16.F04 - OmniCreate+

- Task E16.F04.T01: generazione copy.
- Task E16.F04.T02: generazione asset e contenuti.
- Task E16.F04.T03: automazione email/post/task.

### Feature E16.F05 - OmniWallet+, OmniRewards+, OmniShop+

- Task E16.F05.T01: consolidare wallet cross-modulo.
- Task E16.F05.T02: rendere reward trasversale a tutto l'ecosistema.
- Task E16.F05.T03: introdurre marketplace diretto partner.

### Feature E16.F06 - OmniInsights+, OmniSupply+, OmniNetwork+

- Task E16.F06.T01: insight avanzati enterprise.
- Task E16.F06.T02: automazione supply chain.
- Task E16.F06.T03: networking e collaborazione tra aziende.

---

## Epic E17 - Sanita integrata nazionale (`P2`)

### Feature E17.F01 - Integrazioni istituzionali

- Task E17.F01.T01: integrare FSE in modo piu completo.
- Task E17.F01.T02: integrare SSN/ASL.
- Task E17.F01.T03: integrare strutture private e ospedali.

### Feature E17.F02 - Salute avanzata

- Task E17.F02.T01: monitoraggio pazienti cronici.
- Task E17.F02.T02: wearable e dispositivi salute.
- Task E17.F02.T03: moduli nutrizione, sonno, salute mentale.
- Task E17.F02.T04: moduli fertilita, gravidanza, caregiver, anziani, disabilita.
- Task E17.F02.T05: visite domiciliari e assistenza a casa.

### Feature E17.F03 - Ricerca e welfare

- Task E17.F03.T01: trial clinici.
- Task E17.F03.T02: welfare sanitario.
- Task E17.F03.T03: campagne di prevenzione e screening pubblici.

---

## Epic E18 - Commerce/finance avanzati (`P2`)

### Feature E18.F01 - Marketplace e orchestrazione ordini

- Task E18.F01.T01: estendere orchestration checkout partner dove legale e sostenibile.
- Task E18.F01.T02: consolidare tracking economico per famiglia/piano/canale.
- Task E18.F01.T03: introdurre moduli di negoziazione tra parti.

### Feature E18.F02 - Supply e procurement

- Task E18.F02.T01: automazione riordini enterprise.
- Task E18.F02.T02: segnali domanda/offerta per procurement.
- Task E18.F02.T03: forecast inventory multi-canale.

---

## Epic E19 - Funzioni avanzate e R&D (`P2`)

### Feature E19.F01 - Esperienze immersive

- Task E19.F01.T01: AR/VR per settori selezionati.
- Task E19.F01.T02: configuratori 3D.

### Feature E19.F02 - Retail tech

- Task E19.F02.T01: IoT retail.
- Task E19.F02.T02: digital twin.
- Task E19.F02.T03: smart home/riordino automatico.

### Feature E19.F03 - Fiducia e certificazione

- Task E19.F03.T01: blockchain/certificazioni autenticita.
- Task E19.F03.T02: sostenibilita e provenienza prodotto.

### Feature E19.F04 - Analisi avanzate

- Task E19.F04.T01: heatmap comportamentali.
- Task E19.F04.T02: mappatura emozionale.
- Task E19.F04.T03: simulazioni what-if.
- Task E19.F04.T04: correlazioni macroeconomiche.

### Feature E19.F05 - Assistente AI

- Task E19.F05.T01: assistente AI personale B2C.
- Task E19.F05.T02: assistente AI operativo B2B.
- Task E19.F05.T03: suggerimenti proattivi cross-modulo.

---

## Epic E20 - Piattaforma, sicurezza e governance (`P0 -> P2 trasversale`)

### Feature E20.F01 - API Gateway e platform layer

- Task E20.F01.T01: introdurre API Gateway.
- Task E20.F01.T02: centralizzare auth, rate limit, logging, routing.

### Feature E20.F02 - Logging, audit e monitoraggio

- Task E20.F02.T01: centralizzare log applicativi.
- Task E20.F02.T02: introdurre audit log.
- Task E20.F02.T03: introdurre dashboard osservabilita.

### Feature E20.F03 - Sicurezza

- Task E20.F03.T01: MFA per account sensibili.
- Task E20.F03.T02: cifratura dati a riposo/in transito.
- Task E20.F03.T03: antifrode.
- Task E20.F03.T04: moderazione contenuti.
- Task E20.F03.T05: compliance e data governance.

### Feature E20.F04 - Admin centrale

- Task E20.F04.T01: pannello admin utenti e aziende.
- Task E20.F04.T02: pannello admin sorgenti.
- Task E20.F04.T03: pannello admin campagne/partner/contenuti.
- Task E20.F04.T04: pannello admin alert e health piattaforma.

---

## Sequenza pratica consigliata

1. E01 -> E02 -> E03
2. E04 -> E05 -> E06
3. E07 -> E08
4. E20 base
5. E09 -> E10 -> E11
6. E12 -> E13
7. E14 -> E15
8. E16 -> E17 -> E18 -> E19

## Sprint iniziali consigliati

### Sprint 1

- E01.F01
- E01.F02
- E02.F01
- E03.F01

### Sprint 2

- E02.F02
- E02.F03
- E03.F02
- E04.F01

### Sprint 3

- E04.F02
- E04.F03
- E05.F01
- E06.F01

### Sprint 4

- E04.F04
- E05.F02
- E06.F02
- E07.F01

### Sprint 5

- E07.F02
- E08.F01
- E08.F02 oppure verticale iniziale alternativo
- E20.F01 base

