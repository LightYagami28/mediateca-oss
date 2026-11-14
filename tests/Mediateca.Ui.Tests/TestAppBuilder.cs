using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Mediateca.Ui;

[assembly: AvaloniaTestApplication(typeof(Mediateca.Ui.Tests.TestAppBuilder))]

namespace Mediateca.Ui.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia();
}
