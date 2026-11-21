# Mediateca

[![CI](https://img.shields.io/github/actions/workflow/status/LightYagami28/mediateca-oss/ci.yml?branch=main&style=flat-square&label=CI)](https://github.com/LightYagami28/mediateca-oss/actions/workflows/ci.yml)
[![CodeQL](https://img.shields.io/github/actions/workflow/status/LightYagami28/mediateca-oss/codeql.yml?branch=main&style=flat-square&label=CodeQL)](https://github.com/LightYagami28/mediateca-oss/actions/workflows/codeql.yml)
[![.NET](https://img.shields.io/badge/.NET-11.0-512BD4?style=flat-square&logo=dotnet)](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/overview)
[![Licenza](https://img.shields.io/badge/licenza-AGPL--3.0--or--later-3f4f74?style=flat-square)](LICENSE)

Reimplementazione indipendente e scritta a mano dell'applicazione desktop
italiana **Teledisk 1.7** (originale proprietario, Python/PyQt5): catalogo di una
libreria media ospitata su Telegram, con database locale, server HTTP/WebDAV,
streaming, bot Telegram e backup.

Il progetto è in .NET 11 con licenza **AGPL-3.0-or-later** e **non è affiliato a
teledisk.net** né supportato dagli autori dell'originale.

## Stato dell'implementazione

| Ambito | Stato |
| --- | --- |
| Percorsi dati, impostazioni JSON, hash password, cifratura dei segreti | pronto |
| Database SQLite con migrazioni versionate e controllo di integrità | pronto |
| Catalogo media e canali, ricerca, statistiche | pronto |
| Import di una cartella locale nel catalogo | pronto |
| Server HTTP con API JSON, WebDAV in sola lettura e streaming con Range | pronto |
| Code di trasferimento persistenti con worker di download | pronto |
| Bot Telegram: long polling, comandi, tastiere inline, amministrazione, Stars, MiniApp, Telegraph | pronto |
| Client MTProto (login, import canali, download in cache) | pronto, end-to-end da verificare con credenziali reali |
| Interfaccia Avalonia 12 (catalogo, canali, impostazioni, log, tema chiaro/scuro) | pronto |
| Scraper TMDB e MusicBrainz, lettura NFO, copertine locali | pronto |
| Backup compresso con ritenzione | pronto |
| `MediatecaSettings.Validate()` e `JsonSettingsStore.LoadStrict()` | pronto |
| Pool di bot e upload MTproto | in libreria, non ancora collegati alla coda |
| `.strm`, radar degli intro, FUSE, scraper Tidal, pacchetto di distribuzione | mancanti |

Confronto dettagliato con l'originale: [docs/PARITA.md](docs/PARITA.md).
Architettura: [ARCHITECTURE.md](ARCHITECTURE.md). Priorità: [ROADMAP.md](ROADMAP.md).

## Architettura

| Progetto | Responsabilità |
| --- | --- |
| `Mediateca.Core` | Dominio, percorsi, impostazioni, hash password, cifratura autenticata |
| `Mediateca.Data` | SQLite: migrazioni, integrità, catalogo, code, import locale |
| `Mediateca.Media` | Analisi nomi, NFO, copertine, scraper TMDB/MusicBrainz |
| `Mediateca.Backup` | Backup compressi con ritenzione |
| `Mediateca.Server` | API HTTP JSON, WebDAV, streaming Range, code di trasferimento |
| `Mediateca.Telegram` / `Mediateca.Mtproto` | Bot e client MTProto |
| `Mediateca.Ui` | Client Avalonia 12 |
| `Mediateca.Cli` | Comandi a riga di comando |

## Compilazione e test

```sh
dotnet restore
dotnet build Mediateca.slnx -c Release
dotnet test Mediateca.slnx -c Release
dotnet list Mediateca.slnx package --vulnerable --include-transitive
```

Build e test attuali: zero errori e zero avvisi, 127 test superati (107 core e
20 UI).

## Sicurezza

- Bind predefinito su `127.0.0.1`; esporre il server richiede una scelta
  esplicita e un termine TLS davanti (il Basic WebDAV viaggia in chiaro).
- Segreti cifrati con la protezione dati dell'utente, mai scritti in chiaro nel
  repository; token API letto da variabile d'ambiente.
- `LoadStrict()` e `SaveValidated()` evitano di persistere o accettare impostazioni
  non valide; gli errori di lettura conservano l'eccezione originale.
- Dettagli e segnalazioni private: [SECURITY.md](SECURITY.md).

## Contribuire e licenza

Regole in [CONTRIBUTING.md](CONTRIBUTING.md), codice di condotta in
[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md). Licenza AGPL-3.0-or-later, testo in
[LICENSE](LICENSE); dipendenze in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
