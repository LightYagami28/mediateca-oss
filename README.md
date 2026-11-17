# Mediateca

[![CI](https://img.shields.io/github/actions/workflow/status/LightYagami28/mediateca-oss/ci.yml?branch=main&style=flat-square&label=CI)](https://github.com/LightYagami28/mediateca-oss/actions/workflows/ci.yml)
[![CodeQL](https://img.shields.io/github/actions/workflow/status/LightYagami28/mediateca-oss/codeql.yml?branch=main&style=flat-square&label=CodeQL)](https://github.com/LightYagami28/mediateca-oss/actions/workflows/codeql.yml)
[![.NET](https://img.shields.io/badge/.NET-11.0-512BD4?style=flat-square&logo=dotnet)](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/overview)
[![Licenza](https://img.shields.io/badge/licenza-AGPL--3.0--or--later-3f4f74?style=flat-square)](LICENSE)

Reimplementazione indipendente, scritta a mano, dell'applicazione desktop italiana **Teledisk 1.7**
(originale proprietario, distribuito come eseguibile Python/PyQt5): catalogo di una libreria media
ospitata su Telegram, con database locale, server HTTP/WebDAV, streaming, bot Telegram e backup.

Il progetto è in .NET 11, con licenza **AGPL-3.0-or-later**, e **non è affiliato a teledisk.net**
né in alcun modo supportato dagli autori dell'originale.

## Stato

| Ambito | Stato |
|---|---|
| Percorsi dati, impostazioni JSON, hash password, cifratura dei segreti | pronto |
| Database SQLite con migrazioni versionate e controllo di integrità | pronto |
| Catalogo media e canali, ricerca, statistica | pronto |
| Import di una cartella locale nel catalogo | pronto |
| Server HTTP con API JSON, WebDAV in sola lettura e streaming dei byte con Range | pronto |
| Code di trasferimento persistenti con worker di download | pronto |
| Bot Telegram: long polling, comandi, tastiere inline, amministrazione utenti, pagamenti Stars, MiniApp, Telegraph | pronto |
| Client Telegram MTProto (login, import canali, download in cache) | pronto (end-to-end da verificare con credenziali) |
| Interfaccia grafica Avalonia 12 (catalogo, canali, impostazioni, log, tema chiaro/scuro) | pronto |
| Scraper TMDB e MusicBrainz, lettura NFO, copertine locali | pronto |
| Backup compresso con ritenzione | pronto |
| Pool di bot (`TelegramBotPool`, a turno con sospensione sui 429) | in libreria, non ancora usato da streaming e upload |
| Upload verso Telegram via MTProto (`TelegramMediaSource.UploadAsync`) | in libreria, end-to-end non verificato |
| `.strm`, radar degli intro | mancanti |
| FUSE (WinFsp su Windows, fuse3 su Linux) | mancante |
| Scraper Tidal | mancante |
| Migrazione dei database dell'originale | mancante |
| Pacchetto di distribuzione (.msi/.zip) | mancante |

Il dettaglio per area, confrontato con i moduli dell'originale, è in [docs/PARITA.md](docs/PARITA.md).

## Architettura

| Progetto | Responsabilità |
|---|---|
| `Mediateca.Core` | Modello di dominio (canali, elementi media), percorsi dati, impostazioni, hash password, cifratura autenticata dei segreti |
| `Mediateca.Data` | Database SQLite: apertura, migrazioni versionate, integrità, catalogo, code di trasferimento, import locale |
| `Mediateca.Server` | Server HTTP locale: API JSON, WebDAV, streaming con Range, MiniApp |
| `Mediateca.Media` | Provider di metadati del catalogo (TMDB, MusicBrainz, NFO, copertine locali) |
| `Mediateca.Telegram` | Client della Bot API: long polling, comandi, pagamenti Stars, MiniApp, Telegraph |
| `Mediateca.Mtproto` | Client MTProto con WTelegramClient: login, import canali, download in cache |
| `Mediateca.Backup` | Backup della radice dati: contratto `IBackupTarget` e destinazione locale con retention, Google Drive |
| `Mediateca.Ui` | Interfaccia grafica Avalonia 12: catalogo, canali, impostazioni, log, server e bot integrati |
| `Mediateca.Cli` | Eseguibile `mediateca`: manutenzione, scansione, backup, server |
| `Mediateca.Tests` / `Mediateca.Ui.Tests` | Test automatici con xUnit |

Descrizione dei flussi e dei percorsi in [docs/ARCHITETTURA.md](docs/ARCHITETTURA.md).

## Requisiti

- .NET SDK **11.0** (la soluzione ha come target `net11.0`; l'interfaccia grafica `net11.0-windows`)
- Windows per l'interfaccia grafica; i progetti di base girano anche su Linux e macOS

## Compilazione

```powershell
dotnet restore
dotnet build Mediateca.slnx -c Release
dotnet test Mediateca.slnx -c Release
```

Un singolo progetto si compila (o si testa) indicandolo, per esempio
`dotnet build src/Mediateca.Cli/Mediateca.Cli.csproj` o
`dotnet test tests/Mediateca.Tests/Mediateca.Tests.csproj`.

L'eseguibile della CLI si produce con
`dotnet publish src/Mediateca.Cli/Mediateca.Cli.csproj -c Release`.

L'interfaccia grafica si avvia con
`dotnet run --project src/Mediateca.Ui/Mediateca.Ui.csproj`.

## Uso della CLI

```powershell
mediateca doctor                       # ambiente, percorsi e stato della licenza
mediateca db                           # crea o aggiorna il database
mediateca settings show                # legge le impostazioni
mediateca settings set port 8083       # modifica le impostazioni
mediateca settings password <segreto>  # imposta la password WebDAV
mediateca scan D:\Media --channel 1000 # importa una cartella nel catalogo
mediateca backup --keep 14             # archivio compresso della radice dati
mediateca backup --drive               # come sopra, poi caricato su Google Drive
mediateca serve --port 8083            # server HTTP/WebDAV
```

Il comando `serve` accetta `--bind` e `--token` (token Bearer per le API JSON, in alternativa alla
variabile `MEDIATECA_API_TOKEN`). Il server espone `/health`, `/api/stats`, `/api/items`,
`/api/items/{id}/stream`, `/miniapp` e l'area `/dav`. La radice dati è
`%APPDATA%\Mediateca` su Windows, sostituibile con `MEDIATECA_HOME`.

Il backup su Google Drive legge `MEDIATECA_GDRIVE_CLIENT_ID`, `MEDIATECA_GDRIVE_CLIENT_SECRET`,
`MEDIATECA_GDRIVE_REFRESH_TOKEN` (client OAuth desktop con ambito `drive.file`) e, facoltativa,
`MEDIATECA_GDRIVE_FOLDER_ID`; `--keep` vale sia per gli archivi locali sia per quelli remoti.

## Licenza

Il codice è distribuito con licenza **AGPL-3.0-or-later** (vedi [LICENSE](LICENSE)).

**Non esistono chiavi di attivazione né funzioni bloccate**: ogni installazione ha tutte le
funzionalità del programma, per sempre.

## Documentazione

- [docs/ARCHITETTURA.md](docs/ARCHITETTURA.md) — flusso dei dati, database, server, percorsi
- [docs/PARITA.md](docs/PARITA.md) — confronto con i moduli dell'originale
- [ROADMAP.md](ROADMAP.md) — cosa resta da fare
- [FAQ.md](FAQ.md) e [TROUBLESHOOTING.md](TROUBLESHOOTING.md) — domande frequenti e problemi noti
- [GLOSSARY.md](GLOSSARY.md) — termini del progetto
- [docs/adr/](docs/adr/README.md) — decisioni di architettura
- [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) — licenze delle dipendenze
- [CHANGELOG.md](CHANGELOG.md) — cronologia delle versioni
- [CONTRIBUTING.md](CONTRIBUTING.md) — come contribuire
- [SECURITY.md](SECURITY.md) — segnalazioni di sicurezza
- [SUPPORT.md](SUPPORT.md) — dove chiedere aiuto
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) — codice di comportamento

## Nota sull'originale

Teledisk 1.7 è un prodotto proprietario di terzi, distribuito come applicazione desktop Python con
interfaccia PyQt5 e client Telegram Telethon. Questo repository non contiene codice né risorse
dell'originale: è una riscrittura indipendente basata sull'osservazione del comportamento.
`Teledisk` e `teledisk.net` appartengono ai rispettivi titolari.


## Documentazione

- [Indice della documentazione](docs/README.md)
- [Architettura](ARCHITECTURE.md)
- [Roadmap](ROADMAP.md)
