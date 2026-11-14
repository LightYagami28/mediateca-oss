using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Teledisk.Ui.Services;
using Teledisk.Ui.ViewModels;
using Teledisk.Ui.Views;

namespace Teledisk.Ui.Tests;

public class RenderTests
{
    private const string ArtifactDirectory = @"E:\repo\teledisk-oss\artifacts\ui";

    [AvaloniaFact]
    public async Task Finestra_principale_Disegna_il_catalogo()
    {
        var window = CreateWindow(500, ThemeVariant.Dark);
        var viewModel = (MainWindowViewModel)window.DataContext!;
        await viewModel.LoadAsync();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        Directory.CreateDirectory(ArtifactDirectory);
        var path = Path.Combine(ArtifactDirectory, "catalogo-scuro.png");
        SavePng(frame!, path);

        Assert.True(new FileInfo(path).Length > 10_000);
        Assert.Equal(500, viewModel.Rows.Count);
    }

    [AvaloniaFact]
    public async Task Finestra_principale_Disegna_il_tema_chiaro()
    {
        var window = CreateWindow(120, ThemeVariant.Light);
        var viewModel = (MainWindowViewModel)window.DataContext!;
        await viewModel.LoadAsync();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        Directory.CreateDirectory(ArtifactDirectory);
        SavePng(frame!, Path.Combine(ArtifactDirectory, "catalogo-chiaro.png"));
    }

    private static MainWindow CreateWindow(int rows, ThemeVariant variant)
    {
        var theme = new ThemeService();
        theme.Set(variant);
        var viewModel = new MainWindowViewModel(new SampleCatalogSource(rows), NullLogger<MainWindowViewModel>.Instance, theme);
        var options = Options.Create(new UiOptions { SampleCatalogRows = rows });
        return new MainWindow(viewModel, theme, options);
    }

    private static void SavePng(Bitmap bitmap, string path)
    {
        using var stream = File.Create(path);
        bitmap.Save(stream, new PngBitmapEncoderOptions());
    }
}
