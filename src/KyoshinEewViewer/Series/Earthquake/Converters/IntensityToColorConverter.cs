using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinMonitorLib;
using System;
using System.Globalization;

namespace KyoshinEewViewer.Series.Earthquake.Converters;

public class IntensityToColorConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (!Dispatcher.UIThread.CheckAccess())
			return Dispatcher.UIThread.InvokeAsync(() => Convert(value, targetType, parameter, culture)).Result;

		var attr = (parameter as string) ?? "Foreground";
		if (value is not JmaIntensity intensity)
			return new SolidColorBrush((Color)(KyoshinEewViewerApp.Application?.FindResource($"Unknown{attr}") ?? throw new NullReferenceException("震度色リソースを取得できません")));
		return new SolidColorBrush((Color)(KyoshinEewViewerApp.Application?.FindResource($"{intensity}{attr}") ?? throw new NullReferenceException("震度色リソースを取得できません")));
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}

public class LpgmIntensityToColorConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (!Dispatcher.UIThread.CheckAccess())
			return Dispatcher.UIThread.InvokeAsync(() => Convert(value, targetType, parameter, culture)).Result;

		var attr = (parameter as string) ?? "Foreground";
		if (value is not LpgmIntensity intensity)
			return new SolidColorBrush((Color)(KyoshinEewViewerApp.Application?.FindResource($"Unknown{attr}") ?? throw new NullReferenceException("震度色リソースを取得できません")));
		return new SolidColorBrush((Color)(KyoshinEewViewerApp.Application?.FindResource($"{intensity}{attr}") ?? throw new NullReferenceException("震度色リソースを取得できません")));
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}
