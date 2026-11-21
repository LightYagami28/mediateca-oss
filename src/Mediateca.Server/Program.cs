using Mediateca.Data;
using Mediateca.Server;

// Avvio autonomo del server, utile in sviluppo: `dotnet run --project src/Mediateca.Server`.
// L'installazione normale passa dalla CLI, che espone lo stesso server come `mediateca serve`.

var paths = Mediateca.Core.Infrastructure.MediatecaPaths.Default();
paths.EnsureCreated();

var database = new MediatecaDatabase(paths.DatabasePath);
var applied = database.Migrate();

var port = int.TryParse(Environment.GetEnvironmentVariable("MEDIATECA_PORT"), out var parsed) ? parsed : 8083;
var bind = Environment.GetEnvironmentVariable("MEDIATECA_BIND") ?? "127.0.0.1";

var options = MediatecaServer.ApplyNonLoopbackGuards(new ServerOptions
{
    BindAddress = bind,
    Port = port,
    WebDavEnabled = true,
});

if (!string.Equals(options.BindAddress, bind, StringComparison.Ordinal))
{
    Console.Error.WriteLine($"bind {bind} senza token API: ascolto limitato a {options.BindAddress}.");
}

if (!options.WebDavEnabled)
{
    Console.Error.WriteLine("bind non-loopback senza credenziali WebDAV: area WebDAV disattivata.");
}

Console.WriteLine($"mediateca server - schema {applied}, bind {options.BindAddress}:{options.Port}");

await MediatecaServer.RunAsync(options, database);
