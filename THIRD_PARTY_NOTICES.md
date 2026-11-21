# Avvisi sulle dipendenze di terze parti

Elenco dei pacchetti NuGet diretti (versioni da `Directory.Packages.props`) con
la licenza dichiarata nel rispettivo `nuspec`. Le dipendenze transitive seguono le
loro licenze e sono consultabili con `dotnet list package --include-transitive`.

| Pacchetto | Versione | Licenza |
|---|---|---|
| Avalonia | 12.1.6 | MIT |
| Avalonia.Controls.DataGrid | 12.1.2 | MIT |
| Avalonia.Desktop | 12.1.6 | MIT |
| Avalonia.Fonts.Inter | 12.1.6 | MIT |
| Avalonia.Headless | 12.1.6 | MIT |
| Avalonia.Headless.XUnit | 12.1.6 | MIT |
| Avalonia.Skia | 12.1.6 | MIT |
| Avalonia.Themes.Fluent | 12.1.6 | MIT |
| CommunityToolkit.Mvvm | 8.4.2 | MIT |
| coverlet.collector | 6.0.4 | MIT |
| Microsoft.Data.Sqlite | 11.0.0 | MIT |
| Microsoft.NET.Test.Sdk | 18.12.0 | MIT |
| System.Security.Cryptography.ProtectedData | 11.0.0 | MIT |
| WTelegramClient | 4.4.10 | MIT |
| xunit | 2.9.3 | Apache-2.0 |
| xunit.runner.visualstudio | 3.1.5 | Apache-2.0 |
| xunit.v3 | 3.2.2 | Apache-2.0 |

## Catena di fornitura

- Le versioni sono centralizzate in `Directory.Packages.props` con pinning
  transitivo abilitato: nessuna versione implicitamente fluttuante.
- Verificare prima di ogni rilascio:

  ```sh
  dotnet list Mediateca.slnx package --vulnerable --include-transitive
  ```

- Nessun pacchetto deprecato o con vulnerabilità note è presente nello stato
  attuale (verifica del 2026-11-21).
- Le GitHub Action sono pinnate a SHA completo e gli artifact di CI includono
  SBOM generato da `sbom.yml`.

Il codice di Mediateca è distribuito con AGPL-3.0-or-later ([LICENSE](LICENSE)).
