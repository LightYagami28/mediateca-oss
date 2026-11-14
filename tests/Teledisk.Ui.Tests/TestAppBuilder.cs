using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Teledisk.Ui;

[assembly: AvaloniaTestApplication(typeof(Teledisk.Ui.Tests.TestAppBuilder))]

namespace Teledisk.Ui.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia();
}
