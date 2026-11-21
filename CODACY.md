# Note sulle regole Codacy

Questo documento registra le regole Codacy il cui comportamento osservato
non corrisponde alla documentazione della regola. Non contiene soppressioni:
le issue restano aperte e la rimozione richiede una decisione esplicita.

## Regole con falsi positivi confermati

### `Semgrep_codacy.csharp.security.null-dereference`

La regola segnala ogni accesso a membri di una variabile priva di un guard
esplicito `if (x != null)`, anche quando il tipo non è nullable.

Evidenze dalle issue Codacy (istantanea 2026-11-21):

- `ArgumentNullException.ThrowIfNull(item);` in `SqliteMediaStore.cs` è
  segnalata come dereferenza di `item`, benché sia essa stessa un controllo.
- `problems.Add(...)` in `MediatecaSettings.cs` è segnalata perché `problems`
  è un parametro `List<string>` non nullable.
- `name.EndsWith(".mp4", ...)` è segnalata benché `name` sia un `string`
  non nullable con `<Nullable>enable</Nullable>`.

Comportamento verificato con un commit di prova: un guard `if (x != null)`
elimina la segnalazione, mentre `is not null` e `&&` non la eliminano.
Applicare la correzione a tutte le occorrenze richiederebbe centinaia di
guard ridondanti su valori già non nullable. **Richiede conferma** prima di
procedere con una modifica di tale portata.

### `Semgrep_codacy.generic.csharp-lowercase-variables`

La regola richiede nomi di variabili in minuscolo, ma segnala proprietà
pubbliche idiomatiche in PascalCase, per esempio `public int Port { get; init; }`
e `public required long Id { get; init; }`. Rinominarle romperebbe l'API
pubblica e le convenzioni C#. **Richiede conferma**.

### `SonarCSharp_S4261`

La documentazione della regola richiede il suffisso `Async` sui metodi che
restituiscono `Task`/`Task<T>`. L'analizzatore Codacy segnala invece metodi
che hanno già il suffisso, per esempio `HandleAsync`, `LoadAsync`,
`ExecuteAsync`. **Richiede conferma**.

### `markdownlint_MD043`

Il parametro predefinito `headings` è una lista vuota: la regola richiede
che ogni documento non contenga alcun heading. Tutti i file Markdown con un
titolo vengono di conseguenza segnalati. **Richiede conferma** sulla struttura
di heading da imporre.

### `SonarCSharp_S1451`

Il parametro predefinito `headerFormat` è un segnaposto
(`<Your-Product-Name>`, `<Your-Company-Name>`). Nessun header reale può
corrispondervi finché il parametro non viene configurato nel profilo.
**Richiede conferma** sul testo dell'header aziendale.

### `SonarCSharp_S3990`

L'analizzatore segnala l'assenza di `[assembly: CLSCompliant(true)]` su ogni
file sorgente. L'aggiunta dell'attributo a livello di assembly non rimuove
le segnalazioni dagli altri file, quindi sembra richiedere l'attributo nello
stesso file (cosa che causerebbe attributi duplicati in C#). **Richiede
conferma**.

### Regole `Agentlinter_*`

`clarity_undefined-term`, `clarity_priority-signal-missing` e
`structure_modular-files` sono regole soggettive sulla documentazione, non
difetti di codice. **Richiedono conferma**.

## Albero delle decisioni

Nessuna regola è stata disabilitata e non sono state aggiunte soppressioni.
Le issue elencate sopra restano quindi aperte in attesa di una decisione.
