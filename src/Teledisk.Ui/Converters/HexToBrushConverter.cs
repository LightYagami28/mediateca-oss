using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Teledisk.Ui.Converters;

public sealed class HexToBrushConverter : IValueConverter
{
    public static HexToBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string hex && Color.TryParse(hex, out var color)
            ? new SolidColorBrush(color)
            : new SolidColorBrush(Color.FromArgb(80, 140, 150, 170));

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}