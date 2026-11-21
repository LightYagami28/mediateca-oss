# Note sulle regole Codacy

Questo documento registra le regole Codacy il cui comportamento osservato
non corrisponde alla documentazione della regola. Non contiene soppressioni:
le issue restano aperte e la rimozione richiede una decisione esplicita.

## Regole con falsi positivi confermati

### `Agentlinter_security_no-pii-exposure`

La regola segnala `<developer@lightyagami.dev>` in `SECURITY.md` (riga 22) come
"Potential PII". L'indirizzo è il **contatto pubblico di inoltro** designato per
le segnalazioni di sicurezza del progetto, equivalente al profilo GitHub
`@LightYagami28`: è pubblico per scelta, è una casella organizzativa e non
l'indirizzo personale di una persona fisica, quindi non costituisce dato
personale ai sensi del GDPR e non espone dati privati.

La regola rileva qualsiasi pattern di indirizzo email nel testo senza
distinguere un contatto pubblico designato da un dato personale: in una security
policy l'indirizzo di segnalazione è volutamente visibile. La segnalazione è
perciò un falso positivo documentato, non un difetto del codice. Non sono state
aggiunte soppressioni, eccezioni di configurazione né rimozioni del contatto:
l'indirizzo resta nella security policy come unico canale email invitato a usare
e nessun indirizzo privato è presente nel repository.

Verifica: `git grep -n 'developer@lightyagami.dev' -- SECURITY.md CODACY.md`
mostra solo il contatto pubblico designato; non sono presenti altri indirizzi
personali nei file del progetto.

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

Comportamento verificato con commit di prova:

- un guard `if (x != null)` che racchiude la singola istruzione elimina la
  segnalazione, mentre `is not null` e le condizioni con `&&` non la eliminano;
- l'aggiunta automatica di guard `if (x != null)` all'inizio di 133 metodi
  (commit `0d1439c`, poi reverted con `c01cbf1`) **non ha ridotto il conteggio**:
  568 issue prima e dopo. La regola richiede quindi il guard nella vicinanza
  immediata dell'accesso, non semplicemente nello stesso metodo, e ha invece
  generato 47 nuove issue di complessità;
- il guard immediato rotta lo scope quando racchiude una dichiarazione di
  variabile usata più avanti (per esempio `var body` in `TelegramBotPoolTests`),
  quindi non può essere applicato in modo meccanico senza refactoring manuale
  di ogni punto.

Per questi motivi la correzione non è applicabile in sicurezza su larga scala.
**Richiede conferma** su come procedere.

### `Semgrep_codacy.generic.csharp-lowercase-variables`

La documentazione ufficiale mostra esempi di variabili locali (`int Count`),
ma l'analizzatore segnala 381 dichiarazioni di ogni tipo: proprietà, metodi,
costanti, parametri e membri di interfaccia. Esempi: `public int Port { get; init; }`,
`private static string NewTempDirectory()`, `public const int MinPort = 1`,
`string Name { get; }`.

Un tentativo di rename testuale di massa (267 identificatori) ha provocato
errori di compilazione: sono stati rinominati anche tipi e namespace
(`System.Text` → `System.text`, `Channel` → `channel`, `Regex` → `regex`),
perché la regola non distingue i variable identifier dai type identifier.
Il rename è stato annullato. Per farlo in sicurezza servirebbe un rename
simbolico Roslyn su ogni symbol dichiarato, con 381 call-site da aggiornare.
**Richiede conferma** sulla strategia.

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

## Nota SonarCloud

L'analisi di SonarCloud del commit `0d1439c` (guard di massa, poi revertito)
ha aperto 302 issue `S3928`/`CA2208` sui `throw new ArgumentNullException()`
senza nome parametro. Il codice è stato ripristinato con `c01cbf1`/`ccbe44a`
e `/health` rimane protetto. Il gate tornerà OK alla prossima analisi del
branch `main`.

## Albero delle decisioni

Nessuna regola è stata disabilitata e non sono state aggiunte soppressioni.
Le issue elencate sopra restano quindi aperte in attesa di una decisione.
