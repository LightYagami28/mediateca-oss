# Architettura

Documento di riferimento per chi mette mano al codice. Descrive come i pezzi si parlano, dove
vivono i dati e quali punti sono deliberatamente lasciati aperti.

## Flusso dei dati

```mermaid
flowchart LR
    S["Sorgente<br/>(cartella locale, in futuro canali Telegram)"] -->|"import / scansione"| C["Catalogo<br/>SqliteMediaStore"]
    C --> D[("database.db<br/>SQLite")]
    D --> A["API JSON<br/>/health /api/stats /api/items"]
    D --> W["WebDAV in sola lettura<br/>/dav"]
    A --> K["Client<br/>(browser, script, app)"]
    W --> K
    L["LicenseAuthority"] -.->|"gate su LicenseFeatures.RemoteAccess"| A
    L -.-> W
```

Il verso è unico: la sorgente alimenta il catalogo, il catalogo alimenta il database, il server legge
il database e non lo modifica mai. La scrittura avviene solo attraverso i comandi di import e le
operazioni esplicite. Così l'area WebDAV può restare in sola lettura per costruzione, non per
convenzione.

## Componenti

| Componente | Ruolo |
|---|---|
| `Mediateca.Core.Domain` | Tipi del dominio: `Channel`, `MediaItem`, `MediaKind`, `ChannelKind`. |
| `Mediateca.Core.Infrastructure.MediatecaPaths` | Unica fonte dei percorsi; tutto ciò che è scrivibile deriva da `Root`. |
| `Mediateca.Core.Abstractions` | `MediatecaSettings` e `ISettingsStore`: impostazioni tipizzate, nessun percorso scritto nel codice. |
| `Mediateca.Core.Settings.JsonSettingsStore` | Persistenza su JSON con scrittura atomica (file temporaneo e sostituzione). |
| `Mediateca.Core.Security` | `PasswordHasher` (PBKDF2-HMAC-SHA256, formato versionato) e `SecretBox` (AES-256-GCM). |
| `Mediateca.Data.MediatecaDatabase` | Apertura del database, migrazioni versionate in transazione, `PRAGMA integrity_check`. |
| `Mediateca.Data.SqliteMediaStore` | Lettura e scrittura del catalogo, ricerca e statistica. |
| `Mediateca.Server.MediatecaServer` | Server HTTP: API JSON e area WebDAV in sola lettura. |
| `Mediateca.Licensing` | Documento, envelope, firma, verifica, chiavi fidate, entitlement effettivo. |
| `Mediateca.Media` | Contratto dei provider di metadati (`IMediaScraper`), registro e client TMDB. |
| `Mediateca.Backup` | Contratto delle destinazioni di backup (`IBackupTarget`), destinazione locale su ZIP con retention e destinazione Google Drive. |
| `Mediateca.Telegram` | Client della Bot API di Telegram (`getMe`, invio messaggi, lettura aggiornamenti) e modelli. |
| `Mediateca.Cli` | Comandi `machine`, `doctor`, `db`, `settings`, `scan`, `backup`, `serve`, `license`. |

## Percorsi dati

Tutti i percorsi derivano da un'unica radice, `MediatecaPaths.Root`. La radice si risolve in
quest'ordine:

1. variabile `MEDIATECA_HOME`, se impostata;
2. `%APPDATA%\Mediateca` su Windows, `~/.local/share/Mediateca` altrove.

| Percorso | Contenuto |
|---|---|
| `<radice>/database.db` | Catalogo, canali, code di trasferimento, backup, log applicativi. |
| `<radice>/settings.json` | Impostazioni dell'utente. |
| `<radice>/license.tlicense` | Licenza opzionale dell'installazione. |
| `<radice>/keys/trusted/` | Chiavi pubbliche aggiuntive di cui fidarsi. |
| `<radice>/backups/` | Archivi ZIP dei backup, con data e ora nel nome e ritenzione configurabile. |
| `<radice>/cache/` | Copertine e anteprime. |
| `<radice>/logs/` | Log applicativi. |

Le cartelle vengono create da `MediatecaPaths.EnsureCreated`, invocato dai comandi prima di qualunque
scrittura. Il bundle dell'applicazione resta di sola lettura, come nell'originale: nessun dato
dell'utente finisce nella cartella di installazione.

## Database

SQLite locale, un file per installazione, modalità WAL. La versione dello schema è in
`PRAGMA user_version` e le migrazioni sono idempotenti e applicate in una sola transazione:
un'interruzione lascia il database alla versione precedente, mai a metà.

| Tabella | Contenuto |
|---|---|
| `channels` | Canali o cartelle di origine della libreria. |
| `media_items` | Voci del catalogo, legate al canale dalla coppia `channel_id`/`message_id`. |
| `transfer_jobs` | Code di trasferimento (campi pronti per download e upload). |
| `backups` | Backup eseguiti. |
| `logs` | Eventi applicativi per livello e ambito. |

`MediatecaDatabase.CheckIntegrity` espone `PRAGMA integrity_check`; `mediateca db` lo richiama e
restituisce `2` se il database non è sano, così un controllo automatico può accorgersene.

## Server

Il server si costruisce con `MediatecaServer.Build` e si avvia con `RunAsync`; la CLI lo espone come
`mediateca serve`, e il progetto `Mediateca.Server` ha un `Program` autonomo per lo sviluppo.

| Rotta | Metodo | Autenticazione | Risposta |
|---|---|---|---|
| `/health` | GET | nessuna | stato, tier, funzionalità, versione schema |
| `/api/stats` | GET | Bearer, se `--token` è impostato | conteggi di elementi, byte e canali |
| `/api/items` | GET | Bearer, se `--token` è impostato | voci del catalogo, filtri `q`, `kind`, `limit` |
| `/dav/...` | `OPTIONS`, `PROPFIND`, `HEAD`, `GET` | Basic, se l'hash della password è impostato | elenco e intestazioni delle voci |

Il bind predefinito è `127.0.0.1`; `0.0.0.0` accetta la rete. Il gate di licenza su
`LicenseFeatures.RemoteAccess` è verificato all'avvio e, quando il bind esce dal loopback e il flag
manca, produce un avviso nel log invece di rifiutare le richieste. Oggi `DefaultFeatures` restituisce
sempre tutti i flag, quindi il gate non scatta mai; la struttura resta pronta.

L'area WebDAV parla il minimo indispensabile di DAV per i client di libreria (rclone, Jellyfin,
Emby): `OPTIONS`, `PROPFIND` con `multistatus`, `HEAD`, `GET`. Il corpo di `GET` è ancora vuoto: il
server espone metadati e dimensioni, non i byte, che nell'originale arrivano dal client Telegram.

## Impostazioni e segreti

`MediatecaSettings` è un record immutabile: le modifiche passano da `with` e da un salvataggio
completo, così il file non si trova mai in stati intermedi incoerenti. La scrittura è atomica.

Le password non si memorizzano in chiaro: `PasswordHasher` produce
`pbkdf2-sha256$iterazioni$salt$hash`, verificabile a tempo costante. I valori da cifrare (per
esempio il numero di telefono associato a una sessione) passano da `SecretBox`, che usa AES-256-GCM
con nonce per ogni valore e stringa autodescrittiva `v1$nonce$tag$payload`.

## Punti aperti

- Client Telegram MTProto: non esiste. C'è il client della Bot API (`Mediateca.Telegram`), che non
  sostituisce Telethon per scaricare o pubblicare file nei canali.
- Il comando `scan` importa da cartella locale; la scansione di un canale Telegram arriverà quando
  ci sarà il client.
- `Mediateca.Media` ha il contratto e il client TMDB, non ancora richiamati dai comandi.
- Nessuna interfaccia grafica: la CLI è l'unico punto di ingresso.
