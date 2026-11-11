# Licenza del software e licenza opzionale

## In breve: nessuna chiave, nessuna funzione bloccata

Teledisk Community è software libero con licenza **AGPL-3.0-or-later** e funziona **completa così
com'è**: non serve alcun file di licenza, non esiste una scadenza e **nessuna funzione è bloccata**.
Tutti i tier concedono le stesse funzionalità (`LicenseAuthority.DefaultFeatures` restituisce
`LicenseFeatures.All` per qualunque tier).

I nomi `Community`, `Pro` e `Patron` sono quindi solo un'etichetta: comodi per riconoscere chi
sostiene il progetto, ma senza differenze tecniche nell'eseguibile. Chi non ha alcuna licenza non
perde nulla.

Il file di licenza è **opzionale** e serve soltanto a:

- dare un'identità all'installazione (intestatario, note, riferimento di ordine);
- fissare una scadenza, quando serve (per esempio licenze di supporto annuale);
- opzionalmente legare la copia a una macchina;
- comparire nei log e nell'output di `teledisk doctor` e `teledisk license status`.

Una licenza non valida, scaduta o legata a un'altra macchina non impedisce l'avvio: l'applicazione
continua con il profilo completo e lo stato viene solo segnalato.

Il resto di questo documento è il riferimento tecnico per chi vuole emettere o verificare una
licenza.

## Tier

| Tier | Significato |
|---|---|
| `Community` | Assenza di licenza o licenza intestata alla comunità. È il caso normale. |
| `Pro` | Etichetta di sostegno. |
| `Patron` | Etichetta di sostegno più alta. |

Il valore viene scritto nel documento e riportato nei log e nello stato, senza cambiare il
comportamento del software.

## Funzionalità (`LicenseFeatures`)

I flag esistono per il futuro: oggi sono tutti attivi in ogni caso. Restano utili per descrivere
un'installazione e per eventuali differenze future.

| Flag | Valore | Significato |
|---|---|---|
| `None` | 0 | Nessuna funzionalità opzionale. |
| `CoreLibrary` | 1 | Libreria locale, server HTTP, WebDAV in sola lettura. |
| `TelegramBot` | 2 | Integrazione bot Telegram. |
| `RemoteAccess` | 4 | Accesso al server da fuori dalla macchina locale (bind non su `127.0.0.1`). |
| `CloudBackup` | 8 | Backup verso destinazioni remote. |
| `MultiBot` | 16 | Più identità bot attive insieme. |
| `MetadataProviders` | 32 | Provider di metadati di terzi (film, serie, musica). |
| `ScheduledBackup` | 64 | Backup pianificati e differenziali. |
| `All` | 127 | Tutti i flag precedenti. |

Nel documento di licenza il campo `features` contiene flag **aggiuntivi** a quelli del tier; con
l'attuale `DefaultFeatures` ogni flag richiesto è comunque già attivo.

## Chiavi

| Elemento | Percorso | Note |
|---|---|---|
| Chiave pubblica ufficiale | `keys/teledisk-community.pub.pem` | pubblica; è anche incorporata nell'applicazione, quindi una licenza si verifica senza rete |
| Chiave privata dell'issuer | **fuori dal repository** | serve solo a emettere licenze; chi la detiene può firmarle tutte. La posizione comoda è `<radice dati>/keys/`, che `license issue` consulta da sé. |
| Chiavi fidate aggiuntive | `<radice dati>/keys/trusted/*.pem` | per firmare licenze proprie senza toccare la chiave del progetto |
| Cartella chiavi fidate alternativa | variabile `TELEDISK_TRUSTED_KEYS` | sostituisce la cartella precedente |

Formato delle chiavi: PEM. La coppia è **RSA-2048**, generata da `teledisk license keygen`
(PKCS#8 per la privata, SPKI per la pubblica). La cartella consigliata è `<radice dati>/keys`, così
`license issue` trova la privata da sé senza `--key`.

Non va confusa con la coppia del tutto separata usata da `Teledisk.LicenseServer`, che parla il
protocollo di attivazione del client originale (RSA PKCS#1 v1.5): una licenza firmata con quella
chiave viene rifiutata, ed è corretto.

L'identificatore della chiave (`kid`) è la chiave pubblica stessa: i primi 16 caratteri esadecimali
dello SHA-256 del suo SubjectPublicKeyInfo. Una chiave malformata non fa fallire la verifica delle
altre: viene solo esclusa.

## Formato del file di licenza

Estensione `.tlicense`, contenuto JSON leggibile:

```json
{
  "v": 1,
  "kid": "c0afc774e6ba5feb",
  "alg": "RSA-PSS-SHA256",
  "payload": "<base64url del documento>",
  "signature": "<base64url della firma>"
}
```

| Campo | Significato |
|---|---|
| `v` | Versione del formato dell'envelope. Solo `1` è accettata. |
| `kid` | Identificatore della chiave che ha firmato: seleziona la chiave pubblica da usare. |
| `alg` | Algoritmo di firma. Solo `RSA-PSS-SHA256` è accettato. |
| `payload` | Documento di licenza, JSON canonico codificato in base64url (senza padding). |
| `signature` | Firma detached del `payload`, base64url, con RSA-PSS e SHA-256. |

Il documento firmato contiene:

| Campo | Significato |
|---|---|
| `id` | Identificativo della licenza (per esempio `tl-20261111-1a2b3c4d`). |
| `product` | Prodotto di destinazione; deve valere `teledisk`. |
| `tier` | Etichetta (`Community`, `Pro`, `Patron`). |
| `features` | Flag aggiuntivi, facoltativi. |
| `subject` | Intestatario, di norma un indirizzo di posta. |
| `issuedAt` | Momento di emissione, UTC. |
| `expiresAt` | Scadenza, UTC; assente significa perpetua. |
| `machine` | Impronta della macchina; vuoto significa licenza portatile. |
| `maxMachines` | Limite indicativo di macchine (informativo). |
| `note` | Nota libera (per esempio il riferimento dell'ordine). |

La verifica è **completamente offline**: serve solo la chiave pubblica. I payload vengono
serializzati in modo canonico, quindi i byte firmati sono riproducibili da qualunque
implementazione. La firma copre l'intero payload, perciò modificare un solo campo invalida il file.

## Comandi

```powershell
teledisk machine                                  # impronta di questa macchina
teledisk license status                            # stato effettivo, in JSON
teledisk license verify .\file.tlicense            # verifica senza installare
teledisk license keygen --out .\chiavi             # coppia di chiavi RSA-2048 (predefinita: cartella corrente)
teledisk license issue --key ... --tier pro ...    # emette una licenza firmata
teledisk license install .\file.tlicense           # installa nella radice dati
```

`license keygen` scrive `teledisk-<kid>-private.pem` e `teledisk-<kid>.pub.pem` e ricorda dove
copiare la chiave pubblica per renderla fidata; ogni esecuzione genera una coppia nuova, con il `kid`
nel nome dei file. `license issue` accetta:

| Opzione | Valore predefinito |
|---|---|
| `--key <privata.pem>` | la prima `*private.pem` in `<radice dati>/keys`, altrimenti errore |
| `--tier <community\|pro\|patron>` | `pro` |
| `--subject <testo>` | vuoto |
| `--expires <yyyy-MM-dd>` | perpetua |
| `--machine <impronta\|this>` | portatile |
| `--features <a,b,c>` | nessuno |
| `--note <testo>` | vuoto |
| `--id <testo>` | generato |
| `--out <file>` | `<id>.tlicense` nella cartella corrente |

Codici di uscita: `0` successo, `1` errore d'uso, `2` verifica fallita.

## Dove viene cercato il file di licenza

In ordine di priorità, il primo file esistente vince:

1. `TELEDISK_LICENSE_PATH` (percorso esplicito);
2. `<TELEDISK_HOME>/license.tlicense`, cioè la radice dati
   (`%APPDATA%\Teledisk` su Windows, `~/.local/share/Teledisk` altrove);
3. `license.tlicense` accanto all'eseguibile.

`TELEDISK_HOME` sostituisce l'intera radice dati. `teledisk doctor` mostra i percorsi in uso e la
cartella dati risolta, utile quando una licenza sembra non essere letta.

## Esempio completo

Emettere una licenza per sé e verificarla:

```powershell
teledisk license keygen --out "$env:APPDATA\Teledisk\keys"
copy "$env:APPDATA\Teledisk\keys\teledisk-*.pub.pem" "$env:APPDATA\Teledisk\keys\trusted\"

teledisk license issue --tier pro --subject nome@esempio.it --expires 2027-12-31 --out .\mialicenza.tlicense

teledisk license verify .\mialicenza.tlicense
teledisk license install .\mialicenza.tlicense
teledisk license status
```

Senza il passaggio in `keys/trusted` la licenza non verifica: il `kid` corrisponderebbe a una chiave
non nota. La cartella viene ricreata al primo avvio dei comandi, quindi basta copiarci il file.
