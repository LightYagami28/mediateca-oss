# Come contribuire

Grazie dell'interesse. Il progetto è piccolo e le regole sono poche: prima di
aprire una modifica di grandi dimensioni conviene aprire una issue per parlarne.

## Preparare l'ambiente

Serve il **.NET SDK 11.0** e nient'altro: nessun generatore di codice, nessun
servizio esterno, nessun database da avviare.

```sh
git clone <url del repository>
cd mediateca-oss
dotnet restore
dotnet build Mediateca.slnx -c Release
dotnet test Mediateca.slnx -c Release
dotnet list Mediateca.slnx package --vulnerable --include-transitive
```

I test stanno in `tests/Mediateca.Tests` e `tests/Mediateca.Ui.Tests` e girano con
xUnit. Una modifica al comportamento va accompagnata da un test che la copre; per
le correzioni, dal test che riproduce il difetto.

## Stile e qualità

Le regole meccaniche sono in [.editorconfig](.editorconfig) (quattro spazi, UTF-8
senza BOM, fine riga LF, newline finale). In sintesi:

- indentazione con quattro spazi, niente tabulazioni;
- `Nullable` e `ImplicitUsings` sono attivi: gli avvisi vanno risolti, non zittiti;
- vietati `#pragma warning disable`, `SuppressMessage`, `NOSONAR`, catch vuoti e
  fallback che mascherano errori;
- i nomi di tipi e membri sono in inglese, commenti XML e documentazione in italiano;
- nessuna riga oltre le 120 colonne;
- spiegare il *perché* di una scelta, non il *cosa* fa il codice ovvio.

## Sicurezza

Ogni file riscritto deve rispettare OWASP Top 10 2025 e NIST CSF 2.0 / SSDF:
autorizzazione server-side deny by default, input validato e output codificato,
query parametrizzate, header HTTP sicuri, TLS 1.3, hash robusti (no MD5/SHA1),
segretti mai hardcoded, log audit senza PII, errori generici senza stack trace,
fail closed. Vedere [SECURITY.md](SECURITY.md).

## Commit

- messaggio in italiano, prima riga breve e all'indicativo;
- un argomento per commit e per pull request;
- nessuna riga `Co-authored-by` e nessuna traccia di generazione automatica;
- niente file generati (`bin/`, `obj/`), nessun segreto;
- se cambia il comportamento, aggiornare `CHANGELOG.md`.

## Pull request

Compila il modello, attendi CI verde e CodeQL. Aggiorna le dipendenze solo a
versioni stabili con migrazioni documentate; mantieni le GitHub Action pinnate a
SHA completo. Partecipando accetti il [codice di condotta](CODE_OF_CONDUCT.md).
