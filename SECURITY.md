# Sicurezza

## Versioni supportate

Il progetto è in sviluppo: si corregge l'ultima revisione di `main`. Non ci sono ancora rilasci
stabili, quindi non c'è manutenzione di rami vecchi.

## Segnalare un problema

Il canale preferito è una **security advisory privata** su GitHub (scheda *Security* →
*Report a vulnerability*): resta riservata finché non è pubblicata la correzione. Se il problema non
è sensibile, va bene anche una issue normale. In alternativa si può scrivere a
**maule2703@gmail.com**.

Nella segnalazione servono: cosa succede, come riprodurlo, versione o commit, sistema operativo, e
se il problema è già pubblico altrove. Non allegare chiavi private né database reali se non
necessario.

I tempi sono quelli di un progetto amatoriale: una prima risposta entro pochi giorni, la correzione
quando è pronta. Chi segnala viene citato nel changelog, se lo desidera.

## Superficie e punti noti

Cose da tenere presenti quando si espone il server oltre `127.0.0.1`:

- **Nessun TLS integrato.** Il server parla HTTP. In rete locale va bene; su Internet serve un
  termine TLS davanti, altrimenti le credenziali Basic del WebDAV viaggiano in chiaro.
- **WebDAV con autenticazione Basic.** La password è memorizzata come hash PBKDF2, ma il confronto
  avviene in chiaro sul filo: senza TLS, il Basic è leggibile da chi ascolta.
- **API JSON con token Bearer facoltativo.** Se il server ascolta oltre il loopback conviene
  impostare `MEDIATECA_API_TOKEN` (o `serve --token`): senza token, `/api/*` risponde a chiunque
  raggiunga la porta.
- **WebDAV in sola lettura.** Il server non modifica il catalogo: la superficie di scrittura è nei
  comandi, non nelle rotte.
- **Bind di default su `127.0.0.1`.** Aprire su `0.0.0.0` è una scelta esplicita dell'utente.
- **Segreti fuori dal repository.** Il token API e l'ApiHash di Telegram sono cifrati con la
  protezione dati dell'utente Windows prima di essere salvati; la chiave API di TMDB si legge da
  `MEDIATECA_TMDB_API_KEY` o dalle impostazioni.

Cose volutamente *non* difese: nessun limite di frequenza sulle richieste del catalogo, nessun
audit degli accessi. Sono margini accettati per un'applicazione personale; chi la espone pubblicamente
lo faccia sapendolo. Il server limita invece i tentativi Basic falliti e tiene il bind sul loopback
senza token.

## Verifiche del pacchetto

Il codice è distribuito con licenza AGPL-3.0-or-later, nel file [LICENSE](LICENSE). Non esistono
chiavi di attivazione né funzioni bloccate: ogni installazione ha tutte le funzionalità.
