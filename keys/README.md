# Chiavi

Qui vive **solo materiale pubblico**. Le chiavi private stanno fuori dal repository, così il repo
resta pubblicabile al 100% e chi clona non trova mai segreti.

## Le due coppie, e perché sono diverse

| Coppia | Dove sta la privata | A cosa serve | La pubblica nel repo |
|---|---|---|---|
| **Issuer del progetto** | `%APPDATA%\Teledisk\keys\teledisk-community-private.pem` | firma le **licenze** del nuovo software (.NET). Verifica: `teledisk license verify` | `keys/teledisk-community.pub.pem`, incorporata in `ProjectKeys.CommunityIssuer` |
| **Server di attivazione** | `%APPDATA%\Teledisk\license-server\issuer-private.pem` | cifra/decifra l'identità (telefono, Telegram ID) nel protocollo del **client originale** `teledisk.exe`; la sua pubblica viene copiata come `pubblic.pem`/`public.pem` accanto all'exe | non serve nel repo |

Le due coppie sono indipendenti di proposito: una firma licenze nostre (RSA-PSS/SHA-256), l'altra
parla il protocollo dell'exe (RSA PKCS#1 v1.5). Una licenza firmata con la chiave del server viene
infatti rifiutata con `SignatureInvalid` dalle build con la chiave del progetto, ed è corretto.

Kid attualmente attesi:

- issuer del progetto: `c0afc774e6ba5feb`
- server di attivazione: `7e91c3bfc2681b82`

## Emettere una licenza (opzionale: il software è completo anche senza)

```powershell
# il comando usa da sé la prima *private.pem in %APPDATA%\Teledisk\keys
teledisk license issue --tier patron --subject "nome" --expires 2027-12-31 --machine this --out mia.tlicense
teledisk license verify mia.tlicense
teledisk license install mia.tlicense     # copia in <TELEDISK_HOME>/license.tlicense
```

Per rigenerare la coppia del progetto:

```powershell
teledisk license keygen --out "$env:APPDATA\Teledisk\keys" --force
# poi ricopiare la new .pub.pem in keys/teledisk-community.pub.pem e aggiornare
# ProjectKeys.CommunityIssuer in src/Teledisk.Licensing/TrustedKeys.cs
```

## Chiavi fidate aggiuntive

Chiunque può emettere licenze per le proprie build senza toccare la chiave del progetto: basta
mettere una pubblica in `<TELEDISK_HOME>\keys\trusted\*.pem` (oppure indicare una cartella con
`TELEDISK_TRUSTED_KEYS`). Una chiave malformata non blocca le altre.
