# Roadmap

Stato reale al 2026-11-21. Il confronto modulo per modulo con l'originale è in
[docs/PARITA.md](docs/PARITA.md); qui sono elencati solo i lavori rimasti, in
ordine di priorità.

## Stato attuale

- Solution `Mediateca.slnx` su .NET 11 con nove progetti, UI Avalonia inclusa.
- Build e test puliti: 127 test totali (107 core + 20 UI), zero errori e zero warning.
- `MediatecaSettings` espone `Validate()` con soglie di porta, scala UI, formato
  `HH:mm`, URL pubblico, tema ed estensioni; `JsonSettingsStore` offre
  `LoadStrict()` e `SaveValidated()` con errori espliciti e `InnerException`.
- La lettura best-effort registra su `Trace` le eccezioni attese invece di
  nasconderle; la scrittura resta atomica con file temporaneo e rename.
- Nessuna chiave di attivazione o funzione bloccata, per scelta progettuale.

## Obiettivi

1. Mantenere build e test con zero errori e zero warning, senza soppressioni.
2. Collegare il pool di bot e l'upload MTProto alla coda di trasferimento persistente.
3. Completare la pubblicazione del catalogo su Telegraph e il radar degli intro.
4. Documentare e validare il formato dei database mobili importati.
5. Migliorare la postura di sicurezza: segreti solo in store dedicati, log senza
   dati sensibili, dipendenze aggiornate e verificabili.

## Prossimi passi

1. **Pool di bot e upload nella coda di trasferimento.** `TelegramBotPool` e
   `TelegramMediaSource.UploadAsync` esistono in libreria ma streaming e upload non
   li usano ancora: vanno collegati a `TransferStore` e `TransferQueueService`.
2. **`.strm` e radar degli intro.**
3. **Catalogo completo su Telegraph.** Oggi c'è solo il client `TelegraphClient`.
4. **Import del database mobile** (`mediateca_media*.db`): serve un file di esempio.
5. **Verifica end-to-end di MTProto**: richiede credenziali Telegram reali
   (ApiId/ApiHash); mai eseguita contro il servizio.
6. **Pacchetto di distribuzione** (`.msi` o `.zip`) con manifest e hash.
7. **FUSE**: WinFsp su Windows e fuse3 su Linux, con pacchetti nativi.
8. **Client Linux e macOS nativi**, dopo il completamento del client Windows.
9. **Scraper Tidal** e migrazione dei database dell'originale.

## Sicurezza e catena di fornitura

- Le dipendenze sono centralizzate in `Directory.Packages.props`; eseguire
  `dotnet list package --vulnerable` a ogni aggiornamento.
- Nessun segreto nei file di configurazione: credenziali e token restano fuori dal
  repository e sono caricati a runtime.
- I log non devono contenere token, password, hash di password o dati personali.
- Le GitHub Action devono essere pinnate a SHA completo; verificare con `gh api`.

## Fatto di recente

Backup su Google Drive, analisi dei nomi dei file, amministrazione degli utenti
del bot, pool di bot, upload MTProto, rubrica Telegram, convalida delle impostazioni
e lettura rigorosa della configurazione: vedere [CHANGELOG.md](CHANGELOG.md).

## Non in programma

Chiavi di attivazione o funzioni bloccate: il progetto è completo per tutti, per
scelta (vedi [ADR 0002](docs/adr/0002-nessuna-chiave-di-attivazione.md)).
