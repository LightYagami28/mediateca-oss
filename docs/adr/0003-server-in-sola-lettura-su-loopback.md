# 0003 - Server in sola lettura, sul loopback di default

Stato: accettata (2026-11-14)

## Contesto

Il server HTTP espone catalogo e streaming; può essere raggiunto da altre
macchine ma non ha TLS.

## Decisione
- Bind di default su `127.0.0.1`; il bind fuori dal loopback senza token API è
  riportato a `127.0.0.1`.
- WebDAV in sola lettura e disattivato fuori dal loopback senza password; la
  password è salvata solo
  come hash PBKDF2.
- Token Bearer confrontato a tempo costante; dopo 10 fallimenti Basic risposta 429.
- Intestazioni di sicurezza su API e WebDAV.

## Conseguenze
La superficie di scrittura sta nei comandi, non nelle rotte. Per l'esposizione
pubblica serve un
terminatore TLS davanti (vedi [SECURITY.md](../../SECURITY.md)).
