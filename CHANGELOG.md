# Cronologia delle versioni

Il formato segue [Keep a Changelog](https://keepachangelog.com/it/1.1.0/) e le versioni seguono
[Semantic Versioning](https://semver.org/lang/it/).

## [Non rilasciato]

### Aggiunto

- Backup su Google Drive con `backup --drive`: upload resumable, token OAuth da refresh token,
  cartella di destinazione facoltativa e ritenzione remota.
- Analisi dei nomi dei file (`MediaNameParser`): tipo, titolo, anno, stagione ed episodio, salvati
  nel catalogo da `scan` e dall'import locale.
- Amministrazione degli utenti del bot: tabella `bot_users` (schema v2), comandi `/users`, `/block`,
  `/unblock` e `/admin` riservati agli amministratori, amministratori iniziali da `Ui:BotAdminIds`.
- `TelegramBotPool`: chiamate a turno su più bot, sospensione dei bot limitati secondo `retry_after`;
  `TelegramApiException.RetryAfterSeconds` espone il valore restituito da Telegram.
- Upload MTProto verso un canale importato (`TelegramMediaSource.UploadAsync`), con registrazione nel
  catalogo.
- Rubrica Telegram: tabella `address_book` (schema v3), `AddressBookStore` e import di contatti, gruppi
  e canali via MTProto (`TelegramMediaSource.ImportAddressBookAsync`).

### Modificato

- Il comando `backup` usa `LocalBackupTarget`; gli archivi si chiamano `mediateca-backup-<data>.zip`.

### Corretto

- Il download MTProto legge i messaggi dei canali con `channels.getMessages` invece di
  `messages.getMessages`, che non vede i messaggi dei canali; l'import dei canali applica l'analisi dei nomi.

## [0.1.0] - 2026-11-11

Prima revisione pubblica: il nucleo è utilizzabile da riga di comando, senza interfaccia grafica e
senza client Telegram.

### Aggiunto

- Percorsi dati unificati in `MediatecaPaths`, con radice scelta da `MEDIATECA_HOME` o dalla cartella
  applicativa dell'utente.
- Impostazioni tipizzate su JSON con scrittura atomica e comando `settings`.
- Hash delle password con PBKDF2-HMAC-SHA256 e cifratura autenticata dei segreti con AES-256-GCM.
- Database SQLite con migrazioni versionate in transazione, modalità WAL e controllo di integrità.
- Catalogo media e canali con ricerca, statistica e import da cartella locale (`scan`).
- Server HTTP locale con `/health`, `/api/stats`, `/api/items` e area WebDAV in sola lettura.
- Backup compresso della radice dati con ritenzione configurabile.
- Sistema di licenza offline opzionale: documento, envelope firmato RSA-PSS-SHA256, verifica con
  chiavi fidate, emissione e installazione, senza funzioni bloccate e senza chiave obbligatoria.
- CLI `mediateca` con i comandi `machine`, `doctor`, `db`, `settings`, `scan`, `backup`, `serve` e
  `license`.
- Server di attivazione locale per far funzionare il client originale Teledisk 1.7 senza il servizio
  del vendor.
- Contratto dei provider di metadati, registro e client TMDB.
- Test automatici con xUnit e integrazione continua su Windows.

### Note

- Nessuna interfaccia grafica: l'uso è da riga di comando.
- Nessun client Telegram MTProto e nessun bot: il catalogo si popola da cartelle locali.
- Nessun pacchetto di distribuzione: si compila dai sorgenti.

[0.1.0]: https://github.com/mediateca-community/mediateca/releases/tag/v0.1.0
