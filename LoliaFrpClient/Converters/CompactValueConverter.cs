using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace LoliaFrpClient.Converters;

public sealed class CompactValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var parts = (parameter as string ?? string.Empty).Split('|');
        if (parts.Length != 2) return null;

        var text = value is true ? parts[1] : parts[0];
        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (type == typeof(GridLength)) return GridLength.Parse(text);
        if (type.IsEnum) return Enum.Parse(type, text);

        return System.Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}