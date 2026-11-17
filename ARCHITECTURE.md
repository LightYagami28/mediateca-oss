# Architettura

Mediateca è strutturata come suite modulare in .NET 11, orientata alla separazione netta tra modello di persistenza, servizi di rete, bot API e interfaccia utente.

```text
                           +----------------------+
                           |   Mediateca.Ui       |
                           |  (Avalonia UI 12)    |
                           +----------+-----------+
                                      |
                                      v
+--------------------+     +----------+-----------+     +--------------------+
|  Mediateca.Server  | <-- |   Mediateca.Core     | --> | Mediateca.Telegram |
| (HTTP/WebDAV/JSON) |     | (Modello e Config)   |     | (Bot API / MiniApp)|
+--------------------+     +----------+-----------+     +--------------------+
                                      |
        +-----------------------------+-----------------------------+
        v                             v                             v
+--------------------+     +--------------------+        +-------------------+
|  Mediateca.Data    |     | Mediateca.Mtproto  |        | Mediateca.Media   |
| (SQLite/Migration) |     | (WTelegramClient)  |        | (Scraper Metadata)|
+--------------------+     +--------------------+        +-------------------+
```

| Assembly | Responsabilità |
|---|---|
| `Mediateca.Core` | Tipi di dominio (canali, elementi multimediali), gestione percorsi `%APPDATA%\Mediateca`, storage chiavi cifrato DPAPI/AES-256-GCM. |
| `Mediateca.Data` | Accesso SQLite, migrazioni automatiche versionate con check di integrità PRAGMA, code di trasferimento persistenti. |
| `Mediateca.Server` | Web server asincrono Kestrel/HttpListener con supporto Range HTTP 206 per streaming continuo, endpoint WebDAV sola lettura. |
| `Mediateca.Telegram` | Client Bot API Telegram: polling, dispatcher comandi, gestione MiniApp e pagamenti Stars. |
| `Mediateca.Mtproto` | Connessione MTProto sicura per download e indicizzazione canali media. |
| `Mediateca.Media` | Parser metadati NFO, scraper TMDB/MusicBrainz e download copertine locali. |
| `Mediateca.Backup` | Generatore backup compressi con rotazione cronologica e destinazione locale/GDrive. |
| `Mediateca.Ui` | Client desktop multipiattaforma basato su Avalonia 12 e CommunityToolkit.Mvvm. |
| `Mediateca.Cli` | Interfaccia a riga di comando per operazioni headless di indicizzazione e manutenzione. |
