using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Mediateca.Ui;
using Mediateca.Ui.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddMediatecaUi(builder.Configuration, builder.Environment);

using var host = builder.Build();
App.Services = host.Services;

host.Start();

try
{
    AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace()
        .StartWithClassicDesktopLifetime(args);
}
finally
{
    host.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
}
