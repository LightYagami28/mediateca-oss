# Teledisk Community

[![CI](https://github.com/LightYagami28/teledisk-oss/actions/workflows/ci.yml/badge.svg)](https://github.com/LightYagami28/teledisk-oss/actions/workflows/ci.yml)
[![CodeQL](https://github.com/LightYagami28/teledisk-oss/actions/workflows/codeql.yml/badge.svg)](https://github.com/LightYagami28/teledisk-oss/actions/workflows/codeql.yml)

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
| Bot Telegram: long polling, comandi, tastiere inline, pagamenti Stars, MiniApp, Telegraph | pronto |
| Client Telegram MTProto (login, import canali, download in cache) | pronto (end-to-end da verificare con credenziali) |
| Interfaccia grafica Avalonia 12 (catalogo, canali, impostazioni, log, tema chiaro/scuro) | pronto |
| Scraper TMDB e MusicBrainz, lettura NFO, copertine locali | pronto |
| Sistema di licenza offline, opzionale e senza funzioni bloccate | pronto |
| Backup compresso con ritenzione | pronto |
| Pool di bot, `.strm`, radar degli intro, upload verso Telegram | mancanti |
| FUSE (WinFsp su Windows, fuse3 su Linux) | mancante |
| Scraper Tidal | mancante |
| Migrazione dei database dell'originale | mancante |
| Pacchetto di distribuzione (.msi/.zip) | mancante |

Il dettaglio per area, confrontato con i moduli dell'originale, è in [docs/PARITA.md](docs/PARITA.md).

## Architettura

| Progetto | Responsabilità |
|---|---|
| `Teledisk.Core` | Modello di dominio (canali, elementi media), percorsi dati, impostazioni, hash password, cifratura autenticata dei segreti |
| `Teledisk.Licensing` | Licenza offline: documento, envelope firmato, firma, verifica, chiavi fidate, entitlement effettivo |
| `Teledisk.Data` | Database SQLite: apertura, migrazioni versionate, integrità, catalogo, code di trasferimento, import locale |
| `Teledisk.Server` | Server HTTP locale: API JSON, WebDAV, streaming con Range, MiniApp |
| `Teledisk.Media` | Provider di metadati del catalogo (TMDB, MusicBrainz, NFO, copertine locali) |
| `Teledisk.Telegram` | Client della Bot API: long polling, comandi, pagamenti Stars, MiniApp, Telegraph |
| `Teledisk.Mtproto` | Client MTProto con WTelegramClient: login, import canali, download in cache |
| `Teledisk.Backup` | Backup della radice dati: contratto `IBackupTarget` e destinazione locale con retention |
| `Teledisk.Ui` | Interfaccia grafica Avalonia 12: catalogo, canali, impostazioni, log, server e bot integrati |
| `Teledisk.Cli` | Eseguibile `teledisk`: manutenzione, scansione, backup, server, licenza |
| `Teledisk.LicenseServer` | Server locale di attivazione per far funzionare il **client originale 1.7** senza il servizio del vendor |
| `Teledisk.Tests` / `Teledisk.Ui.Tests` | Test automatici con xUnit |

Descrizione dei flussi e dei percorsi in [docs/ARCHITETTURA.md](docs/ARCHITETTURA.md).

## Requisiti

- .NET SDK **11.0** (la soluzione ha come target `net11.0`; l'interfaccia grafica `net11.0-windows`)
- Windows per l'interfaccia grafica; i progetti di base girano anche su Linux e macOS

## Compilazione

```powershell
dotnet restore
dotnet build Teledisk.slnx -c Release
dotnet test Teledisk.slnx -c Release
```

Un singolo progetto si compila (o si testa) indicandolo, per esempio
`dotnet build src/Teledisk.Cli/Teledisk.Cli.csproj` o
`dotnet test tests/Teledisk.Tests/Teledisk.Tests.csproj`.

L'eseguibile della CLI si produce con
`dotnet publish src/Teledisk.Cli/Teledisk.Cli.csproj -c Release`.

L'interfaccia grafica si avvia con
`dotnet run --project src/Teledisk.Ui/Teledisk.Ui.csproj`.

## Uso della CLI

```powershell
teledisk doctor                       # ambiente, percorsi e stato della licenza
teledisk db                           # crea o aggiorna il database
teledisk settings show                # legge le impostazioni
teledisk settings set port 8083       # modifica le impostazioni
teledisk settings password <segreto>  # imposta la password WebDAV
teledisk scan D:\Media --channel 1000 # importa una cartella nel catalogo
teledisk backup --keep 14             # archivio compresso della radice dati
teledisk serve --port 8083            # server HTTP/WebDAV
```

Il comando `serve` accetta `--bind` e `--token` (token Bearer per le API JSON, in alternativa alla
variabile `TELEDISK_API_TOKEN`). Il server espone `/health`, `/api/stats`, `/api/items`,
`/api/items/{id}/stream`, `/miniapp` e l'area `/dav`. La radice dati è
`%APPDATA%\TelediskCommunity` su Windows, sostituibile con `TELEDISK_HOME`.

## Licenza del software

Non serve alcuna chiave: l'applicazione è completa così com'è e **nessuna funzione è bloccata**
dall'assenza di un file di licenza. Il codice è distribuito con **AGPL-3.0-or-later** (vedi
[LICENSE](LICENSE)).

In più è possibile emettere una licenza propria, opzionale: serve a dare un'identità e una scadenza
all'installazione (utile per supporto, log e distribuzioni personalizzate). I tier `Community`,
`Pro` e `Patron` sono solo un'etichetta: concedono le stesse funzionalità.

```powershell
teledisk license keygen --out "$env:APPDATA\TelediskCommunity\keys"  # coppia di chiavi RSA-2048
teledisk license issue --tier pro --subject nome@esempio.it          # usa la privata trovata lì
teledisk license verify .\tl-....tlicense                            # verifica senza installare
teledisk license install .\tl-....tlicense                           # copia nella radice dati
teledisk license status                                              # stato effettivo
```

Dettagli di formato, percorsi e chiavi fidate in [docs/LICENZA.md](docs/LICENZA.md).

## Documentazione

- [docs/ARCHITETTURA.md](docs/ARCHITETTURA.md) — flusso dei dati, database, server, percorsi
- [docs/LICENZA.md](docs/LICENZA.md) — schema della licenza opzionale e comandi
- [docs/PARITA.md](docs/PARITA.md) — confronto con i moduli dell'originale
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
