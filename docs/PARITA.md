# Parità rispetto all'originale

Confronto per area tra i moduli dell'originale Teledisk 1.7 (analisi statica del pacchetto
`Mediateca-1.7-Setup.exe`) e la reimplementazione in questo repository.

## Da dove vengono i numeri

L'elenco dei moduli è `report/moduli-applicazione.txt` dell'analisi, il conteggio per area è
`report/moduli-per-area.json`. Sono 148 moduli in totale, così ripartiti:

| Area dell'originale | Moduli |
|---|---|
| `plugin/server` | 49 |
| `backend` | 37 |
| `plugin/bot` | 25 |
| `plugin/upload` | 18 |
| `frontend` | 13 |
| `plugin` (file di radice: `__init__`, `bot`, `telegram`) | 3 |
| `plugin/telegram` | 2 |
| `main` | 1 |
| **Totale** | **148** |

I moduli dei bot e del frontend elencano anche i file `__init__` e gli helper: i numeri sono quelli
dell'analisi, non una stima.

## Matrice di parità

Stati: **fatto** (presente e utilizzabile), **parziale** (una parte c'è, il resto no), **mancante**.
Le note dicono sempre dove sta il confine.

### backend (37 moduli)

| Area | Moduli dell'originale | Stato | Note |
|---|---|---|---|
| Database e migrazioni | 6 — `db`, `db_setup`, `db_migrations`, `sqlite_safety`, `save_on_db_files`, `counter_repair` | **parziale** | SQLite con migrazioni versionate in transazione, WAL, `integrity_check`. Manca la migrazione dei database dell'originale e la riparazione dei contatori. |
| Impostazioni, profili, utilità | 6 — `config`, `settings_backend`, `profile_utils`, `utils`, `gui_actions`, `gui_backend` | **parziale** | Impostazioni JSON tipizzate con scrittura atomica e comando `settings`. Mancano profili multipli e i livelli richiesti dalla GUI. |
| Licenza | 1 — `license_check` | **nessuna** | Nessun sistema di licenza, nessuna chiave e nessuna attivazione: ogni installazione ha tutte le funzionalità. |
| Crittografia | 1 — `crypto_utils` | **parziale** | PBKDF2-HMAC-SHA256 e AES-256-GCM al posto di Fernet. Manca il caso d'uso sulle sessioni Telethon, che non ci sono ancora. |
| Sessioni e identità Telegram | 4 — `telegram_auth`, `telethon_session_guard`, `telegram_ids`, `telegram_runtime_activity` | **parziale** | Client MTProto con WTelegramClient in `Mediateca.Mtproto`: login con sessione persistita, import dei canali con access hash, download in cache. End-to-end NON VERIFICATO (servono credenziali Telegram reali). |
| Canali e download Telegram | 9 — `telegram_channel`, `telegram_download`, `telegram_multibot_download`, `download_queue`, `file_handler`, `multipart_files`, `thumbnail_downloader`, `media_caption`, `telegram_caption_sync` | **mancante** | Il catalogo ha già le colonne per canali, messaggi e code di trasferimento, ma nessuno le riempie da Telegram. |
| File manager e backup | 4 — `file_manager`, `channel_backup`, `selective_backup`, `drive_backup` | **parziale** | Comando `backup` su `LocalBackupTarget` (zip con ritenzione) e, con `--drive`, `GoogleDriveBackupTarget`: upload resumable su Drive v3 con token OAuth da refresh token e ritenzione remota. Mancano backup per singolo canale e selezione dei file. |
| Dati dei bot e rubrica | 5 — `bot_access_db`, `bot_channel_access`, `bot_runtime_manager`, `bot_token_registry`, `telegram_address_book_db` | **parziale** | Registro degli utenti del bot con ruoli in `bot_users` (`BotUserStore`). Mancano registro dei token, permessi per canale e rubrica. |
| Import da smartphone | 1 — `smartphone_import` | **parziale** | Import da cartella locale con upsert idempotente e streaming; l'import del database mobile (`mediateca_media*.db`) resta da specificare. |

### plugin/server (49 moduli)

| Area | Moduli dell'originale | Stato | Note |
|---|---|---|---|
| Server HTTP, API e catalogo | 15 — `server`, `server_tab`, `server_ui_helpers`, `port_guard`, `catalog_api`, `media_api`, `media_db`, `media_scanner`, `media_cover_sync`, `media_index_backup`, `media_name_parser`, `caption_backfill`, `config`, `exceptions`, `__init__` | **parziale** | Server HTTP con `/health`, `/api/stats`, `/api/items`, bind configurabile e token Bearer. L'analisi dei nomi (`MediaNameParser`: tipo, titolo, anno, stagione ed episodio) è applicata da `scan` e dall'import locale. Mancano la scansione lato server e le copertine. |
| Streaming | 10 — `raw_getfile_streamer`, `telethon_streamer`, `stream_admission`, `stream_bot_pool`, `stream_events`, `stream_identity`, `stream_routes`, `generate_strm`, `strm_generator_thread`, `skip_intro_radar` | **parziale** | Il `GET` serve i byte con supporto Range (206/416) da sorgenti locali e da Telegram via cache; niente pool di bot, `.strm` e radar degli intro. |
| WebDAV, VFS e FUSE | 6 — `webdav_support`, `webdav_tab`, `fuse_support`, `fuse_tab`, `mediateca_vfs_core`, `windows_longpaths` | **parziale** | WebDAV in sola lettura (`OPTIONS`, `PROPFIND`, `HEAD`) compatibile con i client di libreria. Mancano il corpo di `GET`, il montaggio FUSE/WinFsp e i percorsi lunghi. |
| Scraper | 7 — `scrapers/__init__` e `base_scraper`, `local_image_scraper`, `musicbrainz_scraper`, `nfo_scraper`, ~~`tidal_music_scraper`~~, `tmdb_scraper` | **parziale** | TMDB, MusicBrainz, parser NFO e copertine locali presenti; mancano le immagini remote. Tidal escluso: richiede un abbonamento a pagamento, non ha senso in un progetto gratuito. |
| Libreria TV, bot di libreria, blacklist | 7 — `library_bot_core`, `library_bot_tab`, `library_bot_autostart`, `mediateca_tv_admin_db`, `mediateca_tv_admin_tab`, `blacklist_manager`, `blacklist_manager_ui` | **mancante** | Nessuna pubblicazione della libreria tramite bot né lista di esclusione. |
| Ispezione e metadati MTProto | 4 — `media_inspector_tab`, `media_library_tab`, `media_library_hierarchy_inspector`, `mtproto_metadata` | **mancante** | Nessuna lettura dei metadati dai messaggi. |

### plugin/bot (25 moduli)

| Area | Moduli dell'originale | Stato | Note |
|---|---|---|---|
| Nucleo, comandi, aiuti | 15 — `admin_chat`, `core`, `commands`, `config`, `database`, `login`, `help`, `help_scope`, `app_package`, `bot_ui_helpers`, `ui`, `markdown_editor`, `icon_picker`, `system_commands`, `telegram_features` | **parziale** | Presente in `Mediateca.Telegram` un client essenziale della Bot API (identità del bot, invio di un messaggio, lettura aggiornamenti). Mancano comandi, menu, stato e login. |
| Catalogo e ricerca | 4 — `catalog`, `user_library`, `user_search`, `tmdb` | **fatto** | Comandi `/catalog`, `/search` e `/stats` con long polling e tastiere inline. |
| Pagamenti e amministrazione utenti | 3 — `payments`, `user_admin`, `stat` | **parziale** | `createInvoiceLink` in Telegram Stars, comando `/stats` e amministrazione utenti (`/users`, `/block`, `/unblock`, `/admin`; amministratori iniziali da `Ui:BotAdminIds`, utenti bloccati ignorati). Manca la conferma dei pagamenti. |
| Messaggistica e Telegraph | 3 — `messaging`, `telegraph_catalog`, `telegraph_store` | **parziale** | `TelegraphClient`, pagina MiniApp servita dal server su `/miniapp` e `setChatMenuButton`; mancano il catalogo completo su Telegraph e le risposte inline. |

### plugin/upload (18 moduli)

| Area | Moduli dell'originale | Stato | Note |
|---|---|---|---|
| Code di caricamento e scaricamento, client Telegram | 18 — `db`, `upload_worker`, `upload_bot_pool`, `telegram_upload/*` (client, coda, formattazione didascalie, video) | **parziale** | Coda di trasferimento persistente (`transfer_jobs`) con stati e progresso, worker di download dalle sorgenti; mancano l'upload verso Telegram e il pool di bot. |

### plugin/telegram (2 moduli) e plugin di radice (3)

| Area | Moduli dell'originale | Stato | Note |
|---|---|---|---|
| Operazioni e utilità Telegram | 2 — `telegram/operations`, `telegram/utils` | **mancante** | — |
| `plugin/__init__`, `plugin/bot`, `plugin/telegram` | 3 | **mancante** | Sono i moduli che agganciavano tutto all'applicazione PyQt5. |

### frontend (13 moduli)

| Area | Moduli dell'originale | Stato | Note |
|---|---|---|---|
| Interfaccia PyQt5: file manager, impostazioni, dialoghi, temi, tabelle, import smartphone, rubrica | 13 — `ui`, `filemanager_gui`, `settings_gui`, `dialog_utils`, `modern_theme`, `console_style`, `icon_helpers`, `notes_dialog`, `operation_dialog`, `table_utils`, `smartphone_import_gui`, `telegram_address_book_gui`, `__init__` | **in corso** | Interfaccia in Avalonia 12 su .NET 11 (`src/Mediateca.Ui`): navigazione laterale multi-vista (catalogo, canali, impostazioni, log), catalogo con tabella virtualizzata e ricerca debounced su SQL, pannello di dettaglio, tema chiaro/scuro, scorciatoie da tastiera; server HTTP integrato come `IHostedService`; impostazioni persistenti con scrittura atomica e token cifrato con DPAPI. Progetti futuri: port nativi in Swift per macOS e Linux. |

### main (1 modulo)

| Area | Moduli dell'originale | Stato | Note |
|---|---|---|---|
| Avvio: wizard password, creazione database, autostart dei componenti | 1 — `main` | **parziale** | Il ruolo è coperto dai comandi `doctor`, `db` e `serve`, senza avvio automatico dei componenti. |

## Bilancio

Fatto o utilizzabile: database e migrazioni, catalogo, import locale, server HTTP e WebDAV in sola
lettura, streaming dei byte con Range, code di trasferimento con worker di download, bot Telegram
con long polling e comandi, CLI e test.

Parziale: client MTProto (login e import funzionanti, end-to-end non verificato senza credenziali),
pagamenti Stars e MiniApp (primo giro), scraper (immagini remote), backup (per canale e
selettivo), upload verso Telegram.

Mancante: pool di bot, `.strm` e radar degli intro, FUSE (WinFsp su
Windows, fuse3 su Linux — da fare distro per distro con il sistema di pacchetti di ognuna),
interfaccia per l'import dello smartphone.

Esclusioni deliberate: Tidal richiede API a pagamento (fuori perimetro); il sistema di licenza
originale e le chiavi di attivazione non vengono riprodotti (nessuna chiave, nessuna attivazione).
