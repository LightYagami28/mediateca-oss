using System.Net;
using Mediateca.Core.Infrastructure;
using Mediateca.Data;
using Mediateca.Server;
using Mediateca.Ui.ViewModels;
using Mediateca.Ui.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mediateca.Ui.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMediatecaUi(this IServiceCollection services, ConfigurationManager configuration, IHostEnvironment environment)
    {
        services.AddOptions<UiOptions>()
            .Bind(configuration.GetSection(UiOptions.SectionName))
            .Configure<SettingsService>(MergePersisted)
            .Validate(options => options.WindowWidth >= 800 && options.WindowHeight >= 480, "WindowWidth deve essere almeno 800 e WindowHeight almeno 480.")
            .Validate(options => options.ServerPort is >= 1 and <= 65535, "ServerPort deve essere tra 1 e 65535.")
            .Validate(options => IPAddress.TryParse(options.ServerBindAddress, out _), "ServerBindAddress non è un indirizzo valido.")
            .Validate(options => options.SearchDebounceMs is >= 0 and <= 2000, "SearchDebounceMs deve essere tra 0 e 2000.")
            .ValidateOnStart();

        services.AddSingleton<SettingsService>(provider =>
        {
            var paths = MediatecaPaths.Default();
            paths.EnsureCreated();
            return new SettingsService(paths.SettingsPath);
        });

        services.AddSingleton<LogBuffer>();
        services.AddSingleton<ILoggerProvider>(provider => provider.GetRequiredService<LogBuffer>());
        services.AddLogging(builder => builder.ClearProviders());

        services.AddSingleton(provider =>
        {
            var paths = MediatecaPaths.Default();
            paths.EnsureCreated();
            return new MediatecaDatabase(paths.DatabasePath);
        });

        services.AddSingleton<ThemeService>();
        services.AddSingleton<ICatalogSource, SqliteCatalogSource>();
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<UiOptions>>().Value;
            var paths = MediatecaPaths.Default();
            var sources = new List<IMediaFileSource>
            {
                new LocalMediaFileSource(Path.Combine(paths.Root, "media")),
            };
            if (options.MtprotoApiId > 0 && options.MtprotoApiHash.Length > 0)
            {
                sources.Add(new Mediateca.Mtproto.TelegramMediaSource(
                    new Mediateca.Mtproto.MtprotoOptions
                    {
                        ApiId = options.MtprotoApiId,
                        ApiHash = options.MtprotoApiHash,
                        SessionPath = options.MtprotoSessionPath.Length > 0 ? options.MtprotoSessionPath : Path.Combine(paths.Root, "telegram.session"),
                        CacheDirectory = options.MtprotoCacheDirectory.Length > 0 ? options.MtprotoCacheDirectory : Path.Combine(paths.Root, "cache", "telegram"),
                    },
                    provider.GetRequiredService<MediatecaDatabase>(),
                    provider.GetRequiredService<ILogger<Mediateca.Mtproto.TelegramMediaSource>>()));
            }

            return new Mediateca.Server.CompositeMediaFileSource([.. sources]);
        });
        services.AddSingleton<BotCatalogAdapter>();
        services.AddSingleton<BotStatisticsAdapter>();
        services.AddSingleton<CatalogViewModel>(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<UiOptions>>().Value;
            return new CatalogViewModel(
                provider.GetRequiredService<ICatalogSource>(),
                provider.GetRequiredService<ILogger<CatalogViewModel>>(),
                options.SearchDebounceMs);
        });
        services.AddSingleton<ChannelsViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<LogsViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddHostedService<ServerService>();
        services.AddHostedService<BotService>();

        return services;
    }

    private static void MergePersisted(UiOptions options, SettingsService settings)
    {
        var saved = settings.Load(options);
        options.Theme = saved.Theme;
        options.WindowWidth = saved.WindowWidth;
        options.WindowHeight = saved.WindowHeight;
        options.SampleCatalogRows = saved.SampleCatalogRows;
        options.ServerBindAddress = saved.ServerBindAddress;
        options.ServerPort = saved.ServerPort;
        options.ServerApiToken = saved.ServerApiToken;
        options.WebDavEnabled = saved.WebDavEnabled;
        options.SearchDebounceMs = saved.SearchDebounceMs;
    }
}
