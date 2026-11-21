# Sicurezza

## Versioni supportate

Il progetto è in sviluppo: si corregge l'ultima revisione di `main`. Non ci sono
ancora rilasci stabili, quindi non c'è manutenzione di rami vecchi.

## Segnalare un problema

Il canale preferito è una **security advisory privata** su GitHub (scheda
*Security* → *Report a vulnerability*): resta riservata finché non è pubblicata
la correzione. Se il problema non è sensibile, va bene anche una issue normale.
In alternativa si può contattare il proprietario su GitHub (**@LightYagami28**)
oppure scrivere a <developer@lightyagami.dev>.

Nella segnalazione servono: cosa succede, come riprodurlo, versione o commit,
sistema operativo e se il problema è già pubblico altrove. Non allegare chiavi
private, token o database reali se non necessario.

### Contatto pubblico di sicurezza

<developer@lightyagami.dev> è la **casella pubblica di inoltro** designata per le
segnalazioni di sicurezza del progetto: non è l'indirizzo personale di una
persona fisica e non espone dati privati. È pubblico per scelta, come il profilo
GitHub **@LightYagami28**, ed è l'unico canale email che il progetto invita a
usare. La regola Codacy `Agentlinter_security_no-pii-exposure` lo segnala come
PII perché rileva qualsiasi indirizzo email nel testo, senza distinguere un
contatto pubblico da un dato personale: in una security policy l'indirizzo di
contatto è volutamente visibile. Nessun indirizzo privato è presente nel
repository.

## Superficie e punti noti

Quando si espone il server oltre `127.0.0.1`:

- **TLS integrato con certificato autofirmato**: il server ascolta in HTTPS per
  impostazione predefinita e genera un certificato in memoria a ogni avvio. I client
  devono accettare quel certificato; in caso di esposizione su Internet resta
  preferibile un termine TLS 1.3 con certificato fidato davanti al servizio (A04).
- **WebDAV con autenticazione Basic**: la password è PBKDF2 e il Basic viaggia
  dentro il tunnel TLS; disattivare HTTPS solo su loopback, mai su una rete condivisa.
- **API JSON con token Bearer facoltativo**: se il server ascolta oltre il
  loopback impostare `MEDIATECA_API_TOKEN` (o `serve --token`); senza token,
  `/api/*` risponde a chiunque raggiunga la porta (A01 Broken Access Control).
- **Autorizzazione server-side)**: ogni richiesta è autorizzata lato server con
  deny by default; gli identificatori non danno accesso a risorse di altri utenti
  (no IDOR).
- **Header HTTP sicuri**: CSP, HSTS, X-Content-Type-Options, X-Frame-Options e
  Referrer-Policy devono essere presenti sulle risposte; debug e directory
  listing disattivati in produzione (A02).
- **Injection**: le query SQLite sono parametrizzate; l'output viene codificato
  per il contesto HTML/JS/URL e nessun comando viene concatenato con input utente
  (A05).
- **WebDAV in sola lettura**: il server non modifica il catalogo.
- **Bind predefinito su `127.0.0.1`**: aprire su `0.0.0.0` è una scelta esplicita.
- **Segreti fuori dal repository**: token API e ApiHash Telegram sono cifrati con
  la protezione dati dell'utente Windows; la chiave TMDB si legge da
  `MEDIATECA_TMDB_API_KEY`. Nessuna chiave nei log.

## Framework di riferimento

OWASP Top 10 2025 (A01–A10), NIST CSF 2.0, SSDF SP 800-218. Il progetto non usa
LLM/RAG: OWASP LLM/GenAI 2025 e MITRE ATLAS non si applicano allo stato attuale.

- **A03 Catena di fornitura**: versioni centralizzate, `dotnet list package
  --vulnerable --include-transitive`, GitHub Action pinnate a SHA completo,
  SBOM in CI, zero pacchetti deprecati.
- **A06/A07**: threat model, rate limiting sulle richieste, policy password e
  protezione brute-force sui tentativi Basic falliti.
- **A08 Integrità**: pacchetti di distribuzione firmati e con hash verificabile.
- **A09 Logging**: log audit strutturati senza segreti o PII; alerting sugli
  errori ripetuti.
- **A10 Eccezioni**: fail closed, errori generici al client, nessuna stack trace.

## Verifiche del pacchetto

```sh
dotnet list Mediateca.slnx package --vulnerable --include-transitive
dotnet build Mediateca.slnx -c Release
dotnet test Mediateca.slnx -c Release
```

## Header HTTPS obbligatori

`Mediateca.Server.SecurityHeaders` applica a ogni risposta, errori inclusi:
`Strict-Transport-Security: max-age=31536000; includeSubDomains; preload`,
`Upgrade-Insecure-Requests: 1`, CSP con sole sorgenti `'self'` e
`upgrade-insecure-requests`, `Referrer-Policy: strict-origin-when-cross-origin`,
`X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY` e una
`Permissions-Policy` restrittiva. Le rotte `/api` e `/dav` aggiungono
`Cache-Control: no-store`. HSTS richiede un termine TLS 1.3 davanti al server.

## Applicazione delle checklist JARVIS

- **Checklist siti web**: applicate validazione input lato server, query
  parametrizzate, output encoding, autorizzazione per record, DTO espliciti,
  limiti di richiesta, protezione brute-force, webhook e segreti fuori dal
  repository. Non applicabili: CSRF con cookie (le API usano Bearer token) e
  frontend browser (la UI è Avalonia desktop).
- **Checklist database**: applicate query parametrizzate, transazioni, migrazioni
  versionate, vincoli di integrità, controllo integrità del file, backup con
  ritenzione, segreti non nei log e default restrittivi. Non applicabili per
  l'architettura single-user con file SQLite locale: account/ruoli multipli,
  MFA, repliche, ACL di rete e service account separati.
- **Checklist codice C#**: applicate le sezioni 1, 2, 3, 4, 6, 7, 9, 10, 11, 12,
  13, 14, 15, 16, 19 e 20. Non applicabili: sezione 5 (ASP.NET Core: il server è
  ospitato ma non espone pagine Razor), 8 (processi dinamici) e 18 (P/Invoke).

## Eccezioni HTTPS documentate

- I test xUnit che usano URL `http://127.0.0.1` disattivano esplicitamente HTTPS
  con `UseHttps = false` (oppure `ServerUseHttps = false`) su un bind loopback:
  è una configurazione dichiarata del server, non un'eccezione di sicurezza.
  Il test `Https_ServeConCertificatoAutofirmato` verifica invece il percorso TLS.
- Gli URI `http://schemas.microsoft.com/...` nei file XAML/manifest sono
  identificatori di namespace richiesti dallo schema, non richieste di rete.
- La stringa `http://` nella CSP è usata solo come controllo di assenza nel test
  degli header.

## Valori di test e costanti che non sono segreti

- Le password, i token bot e gli hash API usati negli xUnit sono **fixture fittizie**
  costruite a runtime (per esempio concatenando parti di stringa): non sono
  credenziali reali e non devono essere trattate come segreti da ruotare.
- Le costanti `MEDIATECA_GDRIVE_CLIENT_SECRET`, `MEDIATECA_TMDB_API_KEY` e
  `MEDIATECA_TELEGRAM_BOT_TOKEN` sono **nomi di variabili d'ambiente**, non valori:
  i segreti si leggono sempre dall'ambiente o dalla configurazione cifrata
  dell'utente. Nessun valore reale è presente nel sorgente.
- Le migrazioni SQLite usano istruzioni SQL letterali assegnate direttamente a
  `CommandText`: le tabelle e i pragma non derivano da input utente. Ogni valore
  variabile nelle query applicative passa invece da parametri ADO.NET.
