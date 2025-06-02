using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace KyoshinEewViewer.Converters;

public class ColorConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not string color || !Color.TryParse(color, out var pc))
			return null;
		return pc;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not Color color)
			return null;
		return "#" + color.ToUInt32().ToString("x8", CultureInfo.InvariantCulture);
	}
}
