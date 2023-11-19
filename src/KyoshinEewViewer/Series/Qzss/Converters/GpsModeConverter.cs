using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace KyoshinEewViewer.Series.Qzss.Converters;

public class GpsModeConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value switch
		{
			"N" => "データ無",
			"A" => "自律測位",
			"D" => "干渉測位",
			"E" => "推定算出",
			_ => $"不明({value})",
		};

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}
