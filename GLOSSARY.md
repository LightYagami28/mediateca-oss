# Glossario

| Termine | Significato |
| --- | --- |
| Catalogo | L'elenco di canali ed elementi media salvato in `database.db` (`SqliteMediaStore`). |
| Canale | Origine di un gruppo di elementi: una cartella importata o un canale Telegram. |
| Radice dati | Cartella unica da cui derivano tutti i percorsi (`MediatecaPaths.Root`). |
| Coda di trasferimento | Elenco persistente dei download/upload, consumato dal worker (`TransferQueueService`). |
| Pool di bot | Più bot Telegram usati a turno; un bot limitato (429) viene sospeso per `retry_after` secondi. |
| MTProto | Protocollo nativo di Telegram, usato con WTelegramClient per login, import e trasferimento file. |
| Bot API | API HTTP dei bot Telegram, usata dal long polling e dai comandi. |
| Stars | Valuta di Telegram per i pagamenti dei bot. |
| MiniApp | Pagina web servita su `/miniapp` e aperta dentro Telegram. |
| Telegraph | Servizio di pubblicazione pagine di Telegram. |
| WebDAV | Protocollo di accesso a file via HTTP; qui in sola lettura su `/dav`. |
| Range | Intestazione HTTP per leggere un intervallo di byte (risposte 206/416) usata dallo streaming. |
| DPAPI | Protezione dati dell'utente Windows con cui si cifrano token e ApiHash. |
| CPM | Central Package Management: le versioni dei pacchetti stanno in `Directory.Packages.props`. |
| `.strm` | File testo che punta a uno stream; non ancora supportato. |
