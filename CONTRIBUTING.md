# Come contribuire

Grazie dell'interesse. Il progetto è piccolo e le regole sono poche: prima di aprire una modifica
di grandi dimensioni conviene aprire una issue per parlarne.

## Preparare l'ambiente

Serve il **.NET SDK 11.0** e nient'altro: nessun generatore di codice, nessun servizio esterno,
nessun database da avviare.

```powershell
git clone <url del repository>
cd mediateca-oss
dotnet restore
dotnet build Mediateca.slnx -c Release
dotnet test Mediateca.slnx -c Release
```

I test stanno in `tests/Mediateca.Tests` e girano con xUnit. Una modifica al comportamento va
accompagnata da un test che la copre; per le correzioni, il test che riproduce il difetto.

## Stile

Le regole meccaniche sono in [.editorconfig](.editorconfig) (quattro spazi, UTF-8 senza BOM, fine
riga LF, newline finale). In sintesi:

- indentazione con quattro spazi, niente tabulazioni;
- `Nullable` e `ImplicitUsings` sono attivi: gli avvisi vanno risolti, non zittiti;
- i nomi di tipi e membri sono in inglese, i commenti XML e la documentazione in **italiano**;
- niente righe oltre le 120 colonne;
- nessuna dipendenza nuova se la libreria standard basta; una dipendenza in più va spiegata.

Uno stile di commento ricorrente nel repository: spiegare il *perché* di una scelta, non il *cosa*
fa il codice ovvio. Le scelte delicate stanno nei tag `<remarks>`.

## Commit

- messaggio in italiano, prima riga breve e all'indicativo ("aggiunge il filtro per tipo"), corpo
  quando serve contesto;
- un argomento per commit e per pull request;
- niente file generati (`bin/`, `obj/`), nessun segreto, nessuna licenza `.tlicense` personale;
- se cambia il comportamento, aggiornare `CHANGELOG.md`.

## Pull request

Nella descrizione indicare: cosa cambia, perché, come è stato provato (comandi eseguiti, test
aggiunti). Se la modifica tocca i percorsi dei dati, il formato del database o il formato della
licenza, dirlo esplicitamente: sono i punti in cui serve compatibilità.

La CI compila e lancia i test su Windows; una pull request con la CI rossa non viene unita.

## Licenza dei contributi

Contribuendo accetti che il tuo lavoro sia distribuito con **AGPL-3.0-or-later**, la stessa licenza
del progetto. Non serve firmare nulla: nessun CLA, nessuna cessione.

## Domande

Apri una issue. Per i problemi di sicurezza, vedi [SECURITY.md](SECURITY.md).
