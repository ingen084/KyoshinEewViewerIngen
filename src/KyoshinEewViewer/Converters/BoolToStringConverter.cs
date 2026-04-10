using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace KyoshinEewViewer.Converters;

public class BoolToStringConverter : IValueConverter
{
	public static readonly BoolToStringConverter VolumeIcon = new()
	{
		TrueValue = "\xf6a9",
		FalseValue = "\xf028",
	};

	public string? TrueValue { get; set; }
	public string? FalseValue { get; set; }

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is bool boolValue)
			return boolValue ? TrueValue : FalseValue;
		return FalseValue;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
