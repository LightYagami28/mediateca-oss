using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Teledisk.Ui.Services;
using Teledisk.Ui.ViewModels;

namespace Teledisk.Ui.Views;

public partial class MainWindow : Window
{
    private readonly ThemeService _theme;

    public MainWindow()
        : this(Resolve<MainWindowViewModel>(), Resolve<ThemeService>(), Resolve<IOptions<UiOptions>>())
    {
    }

    public MainWindow(MainWindowViewModel viewModel, ThemeService theme, IOptions<UiOptions> options)
    {
        InitializeComponent();
        _theme = theme;
        Width = options.Value.WindowWidth;
        Height = options.Value.WindowHeight;
        DataContext = viewModel;

        Application.Current!.RequestedThemeVariant = theme.Current;
        theme.Changed += OnThemeChanged;

        Opened += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel model && model.Rows.Count == 0)
            {
                await model.LoadAsync();
            }
        };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.K)
        {
            this.FindControl<TextBox>("SearchBox")?.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && DataContext is MainWindowViewModel viewModel && viewModel.Search.Length > 0)
        {
            viewModel.Search = string.Empty;
            e.Handled = true;
        }
    }

    private void OnThemeChanged(object? sender, Avalonia.Styling.ThemeVariant variant) =>
        Application.Current!.RequestedThemeVariant = variant;

    private static T Resolve<T>()
        where T : notnull =>
        (App.Services ?? throw new InvalidOperationException("Il contenitore dei servizi non è inizializzato.")).GetRequiredService<T>();
}
