# Scorciatoie per i comandi già documentati nel README.
SLN := Mediateca.slnx

.PHONY: restore build test publish-cli run-ui

restore:
	dotnet restore $(SLN)

build: restore
	dotnet build $(SLN) -c Release --no-restore

test: build
	dotnet test $(SLN) -c Release --no-build

publish-cli:
	dotnet publish src/Mediateca.Cli/Mediateca.Cli.csproj -c Release

run-ui:
	dotnet run --project src/Mediateca.Ui/Mediateca.Ui.csproj
