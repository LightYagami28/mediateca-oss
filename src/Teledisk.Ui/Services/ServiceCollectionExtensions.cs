using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Teledisk.Ui.ViewModels;
using Teledisk.Ui.Views;

namespace Teledisk.Ui.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTelediskUi(this IServiceCollection services, ConfigurationManager configuration, IHostEnvironment environment)
    {
        services.AddOptions<UiOptions>()
            .Bind(configuration.GetSection(UiOptions.SectionName))
            .Validate(options => options.WindowWidth >= 800 && options.WindowHeight >= 480, "WindowWidth deve essere almeno 800 e WindowHeight almeno 480.")
            .ValidateOnStart();

        services.AddSingleton<ThemeService>();
        services.AddSingleton<ICatalogSource>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<UiOptions>>().Value;
            return new SampleCatalogSource(options.SampleCatalogRows);
        });
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services;
    }
}
