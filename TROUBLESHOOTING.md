# Risoluzione dei problemi

## `dotnet build` non trova il target `net11.0`

Serve il .NET SDK 11.0 (`dotnet --version` deve iniziare per `11.`). Su Linux e
macOS compila solo
ciò che non è `net11.0-windows`: l'interfaccia grafica richiede Windows.

## La CLI non trova il database o le impostazioni
Controlla la radice dati con `mediateca doctor`. Se hai impostato
`MEDIATECA_HOME`, i file stanno lì;
altrimenti in `%APPDATA%\Mediateca` (Windows) o `~/.local/share/Mediateca`.

## `mediateca serve` ascolta solo su 127.0.0.1 anche con `--bind 0.0.0.0`
È voluto: se manca il token API il bind fuori dal loopback viene riportato a
`127.0.0.1`. Imposta
`MEDIATECA_API_TOKEN` o passa `--token`. Anche il WebDAV resta spento fuori dal
loopback senza
password (`mediateca settings password <segreto>`).

## 401 sulle API, 429 sul WebDAV

401: manca o è sbagliato il token Bearer. 429: dopo 10 tentativi Basic falliti
il server rallenta le
richieste; attendi e riprova con la password giusta.

## Il token del bot o l'ApiHash non si leggono su un'altra macchina
I segreti sono cifrati con la protezione dati dell'utente Windows (DPAPI):
valgono solo per lo stesso
utente sulla stessa macchina. Reimpostali sulla nuova.

## Il login MTProto non parte
Servono ApiId e ApiHash di Telegram. Non sono mai stati provati end-to-end da
questo progetto: apri
una issue con il messaggio di errore (senza codici di accesso).

## La CI Linux non compila l'interfaccia
Corretto: sul runner Linux si compilano e si testano solo i progetti portabili.
