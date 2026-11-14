using Avalonia.Styling;

namespace Mediateca.Ui.Services;

public sealed class ThemeService
{
    private bool _isLight;

    public event EventHandler<ThemeVariant>? Changed;

    public ThemeVariant Current => _isLight ? ThemeVariant.Light : ThemeVariant.Dark;

    public void Toggle()
    {
        _isLight = !_isLight;
        Changed?.Invoke(this, Current);
    }

    public void Set(ThemeVariant variant)
    {
        var light = variant == ThemeVariant.Light;
        if (_isLight == light)
        {
            return;
        }

        _isLight = light;
        Changed?.Invoke(this, Current);
    }
}
