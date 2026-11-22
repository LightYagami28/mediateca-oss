---
title: Revisione delle issue Codacy
---

**Revisione delle issue Codacy.**

Istantanea 2026-11-22: 2056 issue, 54 pattern, tutte belowThreshold.
Error 577, High 7, Warning 315, Info 1157.

Classificazione: 1304 falsi positivi o blocchi di configurazione,
5 issue reali corrette, 747 issue reali restanti.

Nessuna issue e' stata ignorata, soppressa o esclusa dalla configurazione.

**Falsi positivi dimostrati.**

`Semgrep_codacy.csharp.security.null-dereference` - 575 issue

La regola segnala ogni accesso a membri senza un guard `if (x != null)`
nell'immediata vicinanza, anche su ricevitori non nullable.

- `ArgumentNullException.ThrowIfNull(item);` e' segnalata come dereferenza.
- `problems.Add(...)` e' segnalata con `List<string>` non nullable.
- `name.EndsWith(".mp4", ...)` e' segnalata con `Nullable=enable`
  (`Directory.Build.props:6`).
- `reader.GetInt64(6)` e `services.AddSingleton<LogBuffer>()` sono
  segnalate benche il ricevitore non possa essere null.
- Esperimento gia' eseguito nel repository: 133 guard aggiunti
  (commit `0d1439c`, revertito con `c01cbf1`) non hanno ridotto
  il conteggio: 568 prima e dopo, piu' 47 nuove issue di complessita'.

Conclusione: falso positivo sistematico, correzione non sicura.

`Semgrep_codacy.generic.csharp-lowercase-variables` - 387 issue

Tutte le segnalazioni cadono su membri, non su variabili locali.

- 354 membri con modificatore di visibilita' (proprieta', metodi, costanti).
- 30 membri con attributi, es. `[JsonPropertyName] public string Payload`.
- 3 membri di interfaccia, es. `string Name { get; }`.

I nomi PascalCase sono obbligatori per le convenzioni C# su membri pubblici.
Un rename di massa tentato in precedenza ha rinominato anche tipi e
namespace, rompendo la compilazione.

Conclusione: falso positivo, nessuna rinomina applicata.

`SonarCSharp_S3990` - 101 issue

L'attributo `[assembly: CLSCompliant(true)]` e' gia' presente, ma
l'analizzatore segnala la riga 1 di ogni file. Aggiungerlo in ogni file
genererebbe attributi duplicati in C#.

Conclusione: falso positivo dell'analizzatore.

`SonarCSharp_S4261` - 94 issue

Le segnalazioni cadono su metodi che hanno gia' il suffisso corretto,
per esempio `CreateAsync`, `SearchAsync` e `DebounceAndSearchAsync`.

Conclusione: falso positivo.

`Agentlinter_security_no-pii-exposure` - 2 issue High

`SECURITY.md:14` e `SECURITY.md:22` contengono
`<developer@lightyagami.dev>`, il contatto pubblico di inoltro per le
segnalazioni di sicurezza, equivalente a `@LightYagami28`. In una
security policy il contatto deve essere visibile.

Verifica: `git grep -n 'developer@lightyagami.dev' -- SECURITY.md`

Conclusione: falso positivo, contatto mantenuto.

`Semgrep_codacy.yaml.security.hard-coded-tokens` - 1 issue

`.github/workflows/sbom.yml:11` contiene `id-token: write`, che e'
un'autorizzazione GitHub Actions a richiedere un token OIDC, non un
token o una credenziale.

Verifica: `git grep -n 'id-token' -- .github/workflows/sbom.yml`

Conclusione: falso positivo.

Regole `Agentlinter_*` soggettive - 16 issue

`clarity_undefined-term` (14), `clarity_priority-signal-missing` (1) e
`structure_modular-files` (1) esprimono giudizi stilistici su
`SECURITY.md`, non difetti di codice.

**Blocchi di configurazione.**

`SonarCSharp_S1451` - 102 issue

Il parametro `headerFormat` predefinito contiene i segnaposto
`<Your-Product-Name>` e `<Your-Company-Name>`. Nessun header reale puo'
corrispondervi con la configurazione attuale. Modificare il pattern e'
vietato dal mandato.

`markdownlint_MD043` - 26 issue

Il parametro `headings` predefinito e' una lista vuota: la regola
richiede documenti senza alcun titolo. Ogni file Markdown con un titolo
viene segnalato. Rimuovere tutti i titoli o modificare la configurazione
non e' ammesso.

**Issue reali corrette.**

- `SonarCSharp_S131` (2): clausola `default` aggiunta agli switch di
  `BotCommandDispatcher.DispatchAsync` e `WebDav.Handle`.
- `SonarCSharp_S6507` (1): lock su variabile locale sostituito con
  campo `readonly`.
- `SonarCSharp_S6966` (1): `File.WriteAllText` sostituito con
  `await File.WriteAllTextAsync`.
- `SonarCSharp_S3994` (1): parametro `string baseUrl` convertito in
  `Uri baseUri`, con 3 call-site aggiornate.
- `markdownlint_MD022` (13) e `MD032` (1): righe vuote aggiunte
  intorno a titoli e liste in FAQ, TROUBLESHOOTING e ADR 0003.

**Classificazione dei pattern.**

Legenda: FP = falso positivo dimostrato, CF = blocco di configurazione,
R = reale, F = reale corretto.

| Pattern | N | Classe |
| --- | ---: | --- |
| `Semgrep_codacy.csharp.security.null-dereference` | 575 | FP |
| `Semgrep_codacy.generic.csharp-lowercase-variables` | 387 | FP |
| `SonarCSharp_S109` | 237 | R |
| `Lizard_nloc-minor` | 116 | R |
| `SonarCSharp_S1451` | 102 | CF |
| `SonarCSharp_S3990` | 101 | FP |
| `SonarCSharp_S4261` | 94 | FP |
| `markdownlint_MD013` | 76 | R |
| `SonarCSharp_S2360` | 70 | R |
| `SonarCSharp_S2339` | 28 | R |
| `markdownlint_MD043` | 26 | CF |
| `Lizard_ccn-minor` | 25 | R |
| `SonarCSharp_S1172` | 19 | R |
| `markdownlint_MD022` | 17 | F |
| `Lizard_parameter-count-minor` | 15 | R |
| `SonarCSharp_S1192` | 15 | R |
| `SonarCSharp_S2333` | 15 | R |
| `Agentlinter_clarity_undefined-term` | 14 | FP |
| `psscriptanalyzer_psavoidusingwritehost` | 14 | R |
| `SonarCSharp_S1541` | 12 | R |
| `Lizard_ccn-medium` | 11 | R |
| `SonarCSharp_S4055` | 10 | R |
| `SonarCSharp_S1227` | 8 | R |
| `Lizard_nloc-medium` | 6 | R |
| `SonarCSharp_S1186` | 5 | R |
| `Lizard_file-nloc-minor` | 4 | R |
| `SonarCSharp_S6670` | 4 | R |
| `SonarCSharp_S1067` | 3 | R |
| `SonarCSharp_S1128` | 3 | R |
| `SonarCSharp_S2166` | 3 | R |
| `Agentlinter_security_no-pii-exposure` | 2 | FP |
| `Lizard_file-nloc-medium` | 2 | R |
| Semgrep_codacy.csharp.best-practice.misleading-exception-class-name | 2 | R |
| `SonarCSharp_S1301` | 2 | R |
| `SonarCSharp_S131` | 2 | F |
| `SonarCSharp_S2743` | 2 | R |
| `SonarCSharp_S3059` | 2 | R |
| `SonarCSharp_S3264` | 2 | R |
| `SonarCSharp_S3353` | 2 | R |
| `SonarCSharp_S3908` | 2 | R |
| `SonarCSharp_S4018` | 2 | R |
| `SonarCSharp_S4056` | 2 | R |
| `Agentlinter_clarity_priority-signal-missing` | 1 | FP |
| `Agentlinter_structure_modular-files` | 1 | FP |
| `Lizard_ccn-critical` | 1 | R |
| `Semgrep_codacy.yaml.security.hard-coded-tokens` | 1 | FP |
| `SonarCSharp_S103` | 1 | R |
| `SonarCSharp_S1151` | 1 | R |
| `SonarCSharp_S1200` | 1 | R |
| `SonarCSharp_S126` | 1 | R |
| `SonarCSharp_S134` | 1 | R |
| `SonarCSharp_S2737` | 1 | R |
| `SonarCSharp_S3874` | 1 | R |
| `SonarCSharp_S3994` | 1 | F |
| `SonarCSharp_S4040` | 1 | R |
| `SonarCSharp_S4049` | 1 | R |
| `SonarCSharp_S6507` | 1 | F |
| `SonarCSharp_S6966` | 1 | F |
| `markdownlint_MD032` | 1 | F |
