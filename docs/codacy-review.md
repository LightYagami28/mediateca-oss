# Revisione delle issue Codacy

Questo documento registra la classificazione di ogni pattern segnalato da Codacy
nel repository. Le issue classificate come falsi positivi sono dimostrate con
evidenza verificabile; nessuna di esse viene ignorata, soppressa o rimossa dalla
configurazione. Le issue reali vengono corrette nel codice.

Istantanea analizzata: 2026-11-22 — 2056 issue (Error 577, High 7, Warning 315,
Info 1157), tutte `belowThreshold`, ripartite in 54 pattern.

Riepilogo: **1304 FP dimostrati o blocco di configurazione**, **5 issue reali
corrette**, **747 issue reali restanti**.

## Falsi positivi dimostrati

### `Semgrep_codacy.csharp.security.null-dereference` — 575 issue

La regola segnala ogni accesso a membri di una variabile priva di un guard
`if (x != null)` nella vicinanza immediata, anche quando il ricevitore è
non-nullable per il compilatore.

Evidenze:

- `ArgumentNullException.ThrowIfNull(item);` è segnalata come dereferenza di
  `item`, benché sia essa stessa un controllo.
- `problems.Add(...)` è segnalata perché `problems` è un parametro
  `List<string>` non nullable.
- `name.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)` è segnalata benché
  `name` sia `string` non nullable con `<Nullable>enable</Nullable>` in
  `Directory.Build.props:6`.
- `reader.GetInt64(6)` e `services.AddSingleton<LogBuffer>()` sono segnalate
  benché il ricevitore non possa essere null.
- Verifica sperimentale già eseguita nel repository: un guard immediato
  `if (x != null)` elimina la segnalazione, ma `is not null` e le condizioni con
  `&&` non la eliminano. L'aggiunta automatica di guard a 133 metodi
  (commit `0d1439c`, poi revertito con `c01cbf1`) **non ha ridotto il conteggio**:
  568 issue prima e dopo, e ha aperto 47 nuove issue di complessità. Il guard
  immediato inoltre rompe lo scope delle variabili dichiarate nel blocco.

Conclusione: falso positivo sistematico. La correzione meccanica non è sicura e
non riduce le issue; richiederebbe il refactoring manuale di 575 punti con il
rischio di introdurre nuove issue. Documentato, nessuna soppressione aggiunta.

### `Semgrep_codacy.generic.csharp-lowercase-variables` — 387 issue

La regola segnala "variabili con nome maiuscolo", ma tutte le 387 segnalazioni
cadono su **dichiarazioni di membri pubblici**, non su variabili locali:

- 354 membri con modificatore di visibilità (`public required string Title`,
  `public const int Count`, `public string TryDecrypt(...)`, ...);
- 30 membri con attributi (`[JsonPropertyName("payload")] public string Payload`);
- 3 membri di interfaccia (`string Name { get; }`, `bool IsConfigured { get; }`).

Nessuna variabile locale è stata segnalata. Le righe segnalate sono nomi PascalCase
obbligatori per le linee guida C# (proprietà, metodi, tipi e costanti pubbliche).
Un rename in massa tentato in precedenza ha rinominato anche tipi e namespace
(`System.Text` → `System.text`, `Channel` → `channel`) rompendo la compilazione.

Conclusione: falso positivo. Rinominare 387 membri pubblici in minuscolo
causerebbe una breaking change dell'API e violerebbe le convenzioni C#, senza
eliminare la segnalazione in modo affidabile. Documentato, nessuna modifica.

### `SonarCSharp_S3990` — 101 issue

La regola richiede `[assembly: CLSCompliant(true)]`. L'attributo è già presente
a livello di assembly, ma l'analizzatore segnala comunque la riga 1 di ogni
file sorgente (`using ...`) come mancante. L'aggiunta dell'attributo a livello
di assembly non rimuove le segnalazioni per-file; aggiungerlo in ogni file
genererebbe attributi duplicati in C#.

Conclusione: falso positivo dell'analizzatore. Documentato, nessuna soppressione.

### `SonarCSharp_S4261` — 94 issue

La regola richiede il suffisso `Async` sui metodi che restituiscono `Task`.
Le segnalazioni cadono su metodi che hanno **già** il suffisso corretto:
`Task<BackupResult> CreateAsync(...)`,
`public async Task<IReadOnlyList<MediaCandidate>> SearchAsync(...)`,
`private async Task DebounceAndSearchAsync(...)`.

Conclusione: falso positivo. Documentato, nessuna rinomina.

### `Agentlinter_security_no-pii-exposure` — 2 issue High

- File: `SECURITY.md:14` e `SECURITY.md:22`.
- Testo segnalato: `<developer@lightyagami.dev>`.
- È il contatto **pubblico di inoltro** designato per le segnalazioni di
  sicurezza, equivalente al profilo GitHub `@LightYagami28`; in una security
  policy deve essere visibile. Non è un indirizzo personale.
- Verifica: `git grep -n 'developer@lightyagami.dev' -- SECURITY.md CODACY.md`
  mostra solo il contatto pubblico designato.

Conclusione: falso positivo documentato. Il contatto resta nella policy.

### `Semgrep_codacy.yaml.security.hard-coded-tokens` — 1 issue

- File: `.github/workflows/sbom.yml:11`, testo `id-token: write`.
- `id-token: write` è una **autorizzazione** GitHub Actions che consente al job
  di richiedere un OIDC token all'identity provider: non è un token, né una
  credenziale, né un valore segreto.
- Verifica: `git grep -n 'id-token' -- .github/workflows/sbom.yml` mostra solo
  la chiave sotto `permissions`.

Conclusione: falso positivo. Nessuna modifica al workflow.

### Regole `Agentlinter_*` soggettive — 16 issue

`clarity_undefined-term` (14), `clarity_priority-signal-missing` (1) e
`structure_modular-files` (1) sono giudizi stilistici sulla documentazione
(`SECURITY.md`), non difetti di codice o di sicurezza. Non esiste una
correzione oggettiva che non alteri il contenuto della policy. Documentate.

## Blocchi di configurazione (non falsi positivi di codice)

### `SonarCSharp_S1451` — 102 issue

Il parametro predefinito `headerFormat` contiene i segnaposto
`<Your-Product-Name>` e `<Your-Company-Name>`. Nessun header reale può
corrispondervi finché il profilo non viene configurato. Aggiungere un header
legale ai file non soddisfa la regola con la configurazione attuale. Modificare
la configurazione del pattern è vietato dal mandato.

### `markdownlint_MD043` — 26 issue

Il parametro predefinito `headings` è una lista vuota: la regola richiede che i
documenti non contengano alcun heading. Ogni file Markdown con un titolo viene
segnalato (per esempio `ROADMAP.md:1`, `docs/adr/0004-...:1`). Soddisfare la
regola significherebbe rimuovere tutti i titoli dai documenti. Modificare la
configurazione del pattern è vietato dal mandato.

## Issue reali corrette

| Pattern | Issue | Correzione |
|---|---:|---|
| `SonarCSharp_S131` | 2 | Aggiunta clausola `default` agli switch di `BotCommandDispatcher.DispatchAsync` e `WebDav.Handle`. |
| `SonarCSharp_S6507` | 1 | Sostituito `lock` su variabile locale con campo `readonly`. |
| `SonarCSharp_S6966` | 1 | Sostituito `File.WriteAllText` con `await File.WriteAllTextAsync`. |
| `SonarCSharp_S3994` | 1 | Parametro `string baseUrl` convertito in `Uri baseUri`. |

## Classificazione completa dei pattern

| Pattern | Titolo | Issue | Livello | Classificazione |
|---|---|---:|---|---|
| `Semgrep_codacy.csharp.security.null-dereference` | Avoid Null Dereference | 575 | Error | FP dimostrato |
| `Semgrep_codacy.generic.csharp-lowercase-variables` | Enforce Lowercase Variable Names | 387 | Info | FP dimostrato |
| `SonarCSharp_S109` | Avoid Using Magic Numbers | 237 | Info | Reale — da correggere |
| `Lizard_nloc-minor` | Limit Function Length (Minor) | 116 | Info | Reale — da correggere |
| `SonarCSharp_S1451` | Enforce Copyright and License Headers | 102 | Info | Blocco configurazione |
| `SonarCSharp_S3990` | Mark Assemblies as CLS Compliant | 101 | Warning | FP dimostrato |
| `SonarCSharp_S4261` | Enforce Method Names According to Their Synchronicities | 94 | Info | FP dimostrato |
| `markdownlint_MD013` | Enforce Maximum Line Length | 76 | Info | Reale — da correggere |
| `SonarCSharp_S2360` | Avoid Using Optional Parameters | 70 | Warning | Reale — da correggere |
| `SonarCSharp_S2339` | Avoid Using Public Constant Members | 28 | Warning | Reale — da correggere |
| `markdownlint_MD043` | Enforce Required Heading Structure | 26 | Info | Blocco configurazione |
| `Lizard_ccn-minor` | Limit Function Cyclomatic Complexity (Minor) | 25 | Info | Reale — da correggere |
| `SonarCSharp_S1172` | Remove Unused Method Parameters | 19 | Warning | Reale — da correggere |
| `markdownlint_MD022` | Enforce Headings Surrounded by Blank Lines | 17 | Info | Reale — da correggere |
| `Lizard_parameter-count-minor` | Limit Function Parameter Count (Minor) | 15 | Info | Reale — da correggere |
| `SonarCSharp_S1192` | Avoid Duplicated String Literals | 15 | Warning | Reale — da correggere |
| `SonarCSharp_S2333` | Avoid Redundant Modifiers | 15 | Info | Reale — da correggere |
| `Agentlinter_clarity_undefined-term` | Define acronyms and jargon on first use | 14 | Info | FP — regola soggettiva |
| `psscriptanalyzer_psavoidusingwritehost` | Avoid Using Write-Host | 14 | Warning | Reale — da correggere |
| `SonarCSharp_S1541` | Avoid Excessive Complexity in Methods and Properties | 12 | Warning | Reale — da correggere |
| `Lizard_ccn-medium` | Limit Function Cyclomatic Complexity (Medium) | 11 | Warning | Reale — da correggere |
| `SonarCSharp_S4055` | Avoid Passing String Literals as Localized Parameters | 10 | Warning | Reale — da correggere |
| `SonarCSharp_S1227` | Avoid Using Break Statements Except in Switch Cases | 8 | Info | Reale — da correggere |
| `Lizard_nloc-medium` | Limit Function Length (Medium) | 6 | Warning | Reale — da correggere |
| `SonarCSharp_S1186` | Avoid Empty Methods | 5 | Warning | Reale — da correggere |
| `Lizard_file-nloc-minor` | Limit File Length (Minor) | 4 | Info | Reale — da correggere |
| `SonarCSharp_S6670` | Avoid Using Trace.Write and Trace.WriteLine | 4 | Warning | Reale — da correggere |
| `SonarCSharp_S1067` | Avoid overly complex expressions | 3 | Info | Reale — da correggere |
| `SonarCSharp_S1128` | Remove Unnecessary Using Directives | 3 | Info | Reale — da correggere |
| `SonarCSharp_S2166` | Enforce Exception Naming Consistency | 3 | Warning | Reale — da correggere |
| `Agentlinter_security_no-pii-exposure` | Avoid exposing PII in shared agent files | 2 | High | FP dimostrato |
| `Lizard_file-nloc-medium` | Limit File Length (Medium) | 2 | Warning | Reale — da correggere |
| `Semgrep_codacy.csharp.best-practice.misleading-exception-class-name` | Avoid misleading exception class names | 2 | Warning | Reale — da correggere |
| `SonarCSharp_S1301` | Avoid "switch" statements with fewer than 3 "case" clauses | 2 | Info | Reale — da correggere |
| `SonarCSharp_S131` | Enforce default case in switch/select statements | 2 | High | Reale — corretto |
| `SonarCSharp_S2743` | Avoid Using Static Fields in Generic Types | 2 | Warning | Reale — da correggere |
| `SonarCSharp_S3059` | Avoid Members with Higher Visibility than Their Containing Type | 2 | Warning | Reale — da correggere |
| `SonarCSharp_S3264` | Invoke Events | 2 | Info | Reale — da correggere |
| `SonarCSharp_S3353` | Mark Unchanged Variables as const | 2 | Warning | Reale — da correggere |
| `SonarCSharp_S3908` | Use Generic Event Handlers | 2 | Info | Reale — da correggere |
| `SonarCSharp_S4018` | Enforce Usage of All Type Parameters in Method Parameter List | 2 | Info | Reale — da correggere |
| `SonarCSharp_S4056` | Use Overloads with a CultureInfo or IFormatProvider Parameter | 2 | Warning | Reale — da correggere |
| `Agentlinter_clarity_priority-signal-missing` | Require explicit priority signals in rule-heavy files | 1 | Info | FP — regola soggettiva |
| `Agentlinter_structure_modular-files` | Prefer modular file structure | 1 | Warning | FP — regola soggettiva |
| `Lizard_ccn-critical` | Limit Function Cyclomatic Complexity (Critical) | 1 | Error | Reale — da correggere |
| `Semgrep_codacy.yaml.security.hard-coded-tokens` | Avoid hard-coded tokens | 1 | Error | FP dimostrato |
| `SonarCSharp_S103` | Avoid Lines That Are Too Long | 1 | Info | Reale — da correggere |
| `SonarCSharp_S1151` | Avoid switch case clauses with too many lines of code | 1 | Info | Reale — da correggere |
| `SonarCSharp_S1200` | Avoid Classes Coupled to Too Many Other Classes | 1 | Warning | Reale — da correggere |
| `SonarCSharp_S126` | Enforce final else clause in if ... else if constructs | 1 | Info | Reale — da correggere |
| `SonarCSharp_S134` | Avoid Deep Nesting of Control Flow Statements | 1 | Warning | Reale — da correggere |
| `SonarCSharp_S2737` | Avoid catch clauses that only rethrow | 1 | Info | Reale — da correggere |
| `SonarCSharp_S3874` | Avoid Using "out" and "ref" Parameters in Public Methods | 1 | Warning | Reale — da correggere |
| `SonarCSharp_S3994` | Avoid Using String Parameters for URIs | 1 | High | Reale — corretto |
| `SonarCSharp_S4040` | Normalize Strings to Uppercase | 1 | Warning | Reale — da correggere |
| `SonarCSharp_S4049` | Prefer Properties Over Getter Methods | 1 | Info | Reale — da correggere |
| `SonarCSharp_S6507` | Avoid Synchronizing on Local Variables | 1 | High | Reale — corretto |
| `SonarCSharp_S6966` | Use Awaitable Methods in Async Methods | 1 | High | Reale — corretto |
| `markdownlint_MD032` | Enforce Lists Surrounded by Blank Lines | 1 | Info | Reale — da correggere |
