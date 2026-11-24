# Domande frequenti

## Cos'è Mediateca?

Un client desktop e server locale per catalogare una libreria media ospitata su
Telegram: database
SQLite, API JSON, WebDAV in sola lettura, streaming con Range, bot Telegram e
backup. È una
riscrittura indipendente in .NET 11, non affiliata all'applicazione che ha
ispirato il progetto.

## Quali sistemi sono supportati?

L'interfaccia grafica (Avalonia) è per Windows (`net11.0-windows`). I progetti
di base, la CLI e il
server compilano e girano anche su Linux e macOS: la CI li esegue su Windows e
su Linux.

## Che cosa serve per compilarlo?

Solo il .NET SDK 11.0. Nessun servizio esterno, nessun database da avviare.

```powershell
dotnet build Mediateca.slnx -c Release
dotnet test Mediateca.slnx -c Release
```

## Serve una chiave di attivazione?

No. Nessuna chiave, nessuna funzione bloccata: vedi [ADR 0002](docs/adr/0002-nessuna-chiave-di-attivazione.md).

## Dove sono i miei dati?

Nella radice dati: `%APPDATA%\Mediateca` su Windows, `~/.local/share/Mediateca`
altrove. Si cambia
con la variabile `MEDIATECA_HOME`. Contiene `database.db`, `settings.json`, i
backup, la cache e i log.

## Posso esporre il server su Internet?

Il server usa HTTPS con un certificato autofirmato generato a ogni avvio e di
default ascolta solo su `127.0.0.1`. Fuori dal loopback servono un token API
(`MEDIATECA_API_TOKEN` o `--token`) ed è preferibile un terminatore TLS 1.3 con
certificato fidato: vedi [SECURITY.md](SECURITY.md).

## Il client Telegram funziona?

Il codice MTProto (login, import canali, download, upload, rubrica) è completo e
coperto da test, ma
non è mai stato provato contro Telegram con credenziali reali: serve il tuo ApiId/ApiHash.

## Come faccio un backup?

`mediateca backup --keep 14` crea un archivio ZIP nella cartella `backups/`; con
`--drive` lo carica
anche su Google Drive (variabili `MEDIATECA_GDRIVE_*`, vedi [.env.example](.env.example)).

## Dove segnalo un problema o faccio una domanda?

Problemi: issue con i modelli. Domande e idee: le [Discussioni](https://github.com/LightYagami28/mediateca-oss/discussions).
Vulnerabilità: [SECURITY.md](SECURITY.md).
