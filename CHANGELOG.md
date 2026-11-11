# Cronologia delle versioni

Il formato segue [Keep a Changelog](https://keepachangelog.com/it/1.1.0/) e le versioni seguono
[Semantic Versioning](https://semver.org/lang/it/).

## [0.1.0] - 2026-11-11

Prima revisione pubblica: il nucleo è utilizzabile da riga di comando, senza interfaccia grafica e
senza client Telegram.

### Aggiunto

- Percorsi dati unificati in `TelediskPaths`, con radice scelta da `TELEDISK_HOME` o dalla cartella
  applicativa dell'utente.
- Impostazioni tipizzate su JSON con scrittura atomica e comando `settings`.
- Hash delle password con PBKDF2-HMAC-SHA256 e cifratura autenticata dei segreti con AES-256-GCM.
- Database SQLite con migrazioni versionate in transazione, modalità WAL e controllo di integrità.
- Catalogo media e canali con ricerca, statistica e import da cartella locale (`scan`).
- Server HTTP locale con `/health`, `/api/stats`, `/api/items` e area WebDAV in sola lettura.
- Backup compresso della radice dati con ritenzione configurabile.
- Sistema di licenza offline opzionale: documento, envelope firmato RSA-PSS-SHA256, verifica con
  chiavi fidate, emissione e installazione, senza funzioni bloccate e senza chiave obbligatoria.
- CLI `teledisk` con i comandi `machine`, `doctor`, `db`, `settings`, `scan`, `backup`, `serve` e
  `license`.
- Server di attivazione locale per far funzionare il client originale Teledisk 1.7 senza il servizio
  del vendor.
- Contratto dei provider di metadati, registro e client TMDB.
- Test automatici con xUnit e integrazione continua su Windows.

### Note

- Nessuna interfaccia grafica: l'uso è da riga di comando.
- Nessun client Telegram MTProto e nessun bot: il catalogo si popola da cartelle locali.
- Nessun pacchetto di distribuzione: si compila dai sorgenti.

[0.1.0]: https://github.com/teledisk-community/teledisk/releases/tag/v0.1.0
