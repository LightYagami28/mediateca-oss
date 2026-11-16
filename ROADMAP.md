# Roadmap

Stato reale al 2026-11-16. Il dettaglio per modulo rispetto all'originale è in
[docs/PARITA.md](docs/PARITA.md); qui solo ciò che resta da fare, in ordine di priorità.

## Prossimi passi

1. **Pool di bot e upload nella coda di trasferimento.** `TelegramBotPool` e
   `TelegramMediaSource.UploadAsync` esistono in libreria ma streaming e upload non li usano ancora:
   vanno collegati alla coda persistente (`TransferStore`, `TransferQueueService`).
2. **`.strm` e radar degli intro.**
3. **Catalogo completo su Telegraph** (oggi c'è il client `TelegraphClient`, non la pubblicazione del
   catalogo).
4. **Import del database mobile** (`mediateca_media*.db`): il formato non è documentato, serve un file
   di esempio.
5. **Verifica end-to-end di MTProto** (login, import canali, download, upload, rubrica): richiede
   credenziali Telegram reali (ApiId/ApiHash) e non è mai stata eseguita contro Telegram.
6. **Pacchetto di distribuzione** (.msi o .zip).
7. **FUSE**: WinFsp su Windows e fuse3 su Linux, distribuzione per distribuzione col sistema di
   pacchetti di ognuna.
8. **Client Linux e macOS nativi**, dopo il completamento del client Windows. Lavoro lungo.
9. **Scraper Tidal** e migrazione dei database dell'originale.

## Fatto di recente

Backup su Google Drive, analisi dei nomi dei file, amministrazione degli utenti del bot, pool di bot,
upload MTProto, rubrica Telegram: vedi [CHANGELOG.md](CHANGELOG.md).

## Non in programma

Chiavi di attivazione o funzioni bloccate: il progetto è completo per tutti, per scelta
(vedi [ADR 0002](docs/adr/0002-nessuna-chiave-di-attivazione.md)).
