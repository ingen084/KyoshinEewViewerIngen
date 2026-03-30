using Avalonia;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace KyoshinEewViewer.Converters;

/// <summary>
/// Thickness をスケール値で乗算するコンバーター。
/// values[0]: Thickness, values[1]: double (スケール)
/// </summary>
public class ThicknessScaleConverter : IMultiValueConverter
{
	public static readonly ThicknessScaleConverter Instance = new();

	public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
	{
		if (values.Count >= 2 &&
			values[0] is Thickness thickness &&
			values[1] is double scale and > 0)
		{
			return new Thickness(
				thickness.Left * scale,
				thickness.Top * scale,
				thickness.Right * scale,
				thickness.Bottom * scale
			);
		}
		return values.Count > 0 && values[0] is Thickness t ? t : new Thickness();
	}
}
