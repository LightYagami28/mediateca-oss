# Test

La pipeline GitHub Actions descrive i controlli automatizzati correnti e usa .NET 11. Da Windows esegue restore e build della soluzione, poi i test; il job Linux compila ed esegue il progetto `tests/Mediateca.Tests/Mediateca.Tests.csproj` in Release.

Comandi equivalenti documentati per la solution:

```powershell
dotnet restore Mediateca.slnx
dotnet build Mediateca.slnx -c Release
dotnet test Mediateca.slnx --no-build -c Release
```

Per riprodurre il job Linux:

```sh
dotnet build tests/Mediateca.Tests/Mediateca.Tests.csproj -c Release
dotnet test tests/Mediateca.Tests/Mediateca.Tests.csproj --no-build -c Release
```

La pipeline in `.github/workflows/` resta il riferimento per i controlli effettivamente eseguiti.