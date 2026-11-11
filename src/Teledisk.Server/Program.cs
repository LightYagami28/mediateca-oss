using Teledisk.Data;
using Teledisk.Licensing;
using Teledisk.Server;

// Avvio autonomo del server, utile in sviluppo: `dotnet run --project src/Teledisk.Server`.
// L'installazione normale passa dalla CLI, che espone lo stesso server come `teledisk serve`.

var paths = Teledisk.Core.Infrastructure.TelediskPaths.Default();
paths.EnsureCreated();

var database = new TelediskDatabase(paths.DatabasePath);
var applied = database.Migrate();

var authority = LicenseAuthority.CreateDefault(AppContext.BaseDirectory);
var status = authority.Resolve(AppContext.BaseDirectory);

var port = int.TryParse(Environment.GetEnvironmentVariable("TELEDISK_PORT"), out var parsed) ? parsed : 8083;
var bind = Environment.GetEnvironmentVariable("TELEDISK_BIND") ?? "127.0.0.1";

Console.WriteLine($"teledisk server — schema {applied}, tier {status.Tier}, bind {bind}:{port}");

await TelediskServer.RunAsync(
    new ServerOptions { BindAddress = bind, Port = port, WebDavEnabled = true, WebDavRequireAuth = false },
    database,
    authority);
