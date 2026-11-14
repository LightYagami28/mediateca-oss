using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Teledisk.Data;
using Teledisk.Server;
using Teledisk.Ui.Services;
using Teledisk.Ui.ViewModels;
using Teledisk.Ui.Views;

namespace Teledisk.Ui.Tests;

public class RenderTests
{
    private static readonly string ArtifactDirectory =
        Path.Combine(Path.GetTempPath(), "teledisk-ui-artifacts");

    [AvaloniaFact]
    public async Task Finestra_principale_Disegna_il_catalogo()
    {
        var window = CreateWindow(500, ThemeVariant.Dark);
        await ((MainWindowViewModel)window.DataContext!).LoadAsync();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        Directory.CreateDirectory(ArtifactDirectory);
        var path = Path.Combine(ArtifactDirectory, "catalogo-scuro.png");
        SavePng(frame!, path);

        Assert.True(new FileInfo(path).Length > 10_000);
    }

    [AvaloniaFact]
    public async Task Finestra_principale_Disegna_il_tema_chiaro()
    {
        var window = CreateWindow(120, ThemeVariant.Light);
        await ((MainWindowViewModel)window.DataContext!).LoadAsync();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        Directory.CreateDirectory(ArtifactDirectory);
        SavePng(frame!, Path.Combine(ArtifactDirectory, "catalogo-chiaro.png"));
    }

    [AvaloniaFact]
    public async Task Finestra_principale_Disegna_le_impostazioni()
    {
        var window = CreateWindow(50, ThemeVariant.Dark);
        var viewModel = (MainWindowViewModel)window.DataContext!;
        await viewModel.LoadAsync();
        viewModel.SelectNavCommand.Execute(viewModel.Sections.Single(section => section.Title == "Sistema").Items[0]);

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        Directory.CreateDirectory(ArtifactDirectory);
        SavePng(frame!, Path.Combine(ArtifactDirectory, "impostazioni-scuro.png"));
    }

    private static MainWindow CreateWindow(int rows, ThemeVariant variant)
    {
        var theme = new ThemeService();
        theme.Set(variant);
        var source = new SampleCatalogSource(rows);
        var catalog = new CatalogViewModel(source, NullLogger<CatalogViewModel>.Instance, 0);
        var database = new TelediskDatabase(Path.Combine(NewTempDirectory(), "teledisk.db"));
        var channels = new ChannelsViewModel(database, NullLogger<ChannelsViewModel>.Instance);
        var settings = new SettingsViewModel(
            new SettingsService(Path.Combine(NewTempDirectory(), "settings.json")),
            Options.Create(new UiOptions { SampleCatalogRows = rows }),
            theme);
        var buffer = new LogBuffer();
        var logs = new LogsViewModel(buffer);
        var server = new ServerService(
            Options.Create(new UiOptions()),
            database,
            buffer,
            new LocalMediaFileSource(Path.Combine(NewTempDirectory(), "media")));
        var viewModel = new MainWindowViewModel(catalog, channels, settings, logs, theme, server);
        var options = Options.Create(new UiOptions { SampleCatalogRows = rows });
        return new MainWindow(viewModel, theme, options);
    }

    private static string NewTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "teledisk-ui-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void SavePng(Bitmap bitmap, string path)
    {
        using var stream = File.Create(path);
        bitmap.Save(stream, new PngBitmapEncoderOptions());
    }
}
