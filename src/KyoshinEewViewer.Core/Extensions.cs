using Avalonia;
using Avalonia.Media;
using KyoshinMonitorLib;
using Microsoft.Extensions.Logging;
using System;

namespace KyoshinEewViewer.Core;

public static class Extensions
{
	public static double Distance(this Location point1, Location point2)
		=> 6371 * Math.Acos(Math.Cos(point1.Latitude * Math.PI / 180) * Math.Cos(point2.Latitude * Math.PI / 180) * Math.Cos(point2.Longitude * Math.PI / 180 - point1.Longitude * Math.PI / 180) + Math.Sin(point1.Latitude * Math.PI / 180) * Math.Sin(point2.Latitude * Math.PI / 180));

	public static T RequireService<T>(this IServiceProvider provider)
	{
		ArgumentNullException.ThrowIfNull(provider);

		return (T)(provider.GetService(typeof(T)) ?? throw new InvalidOperationException($"Service \"{typeof(T)}\" is NotFound"));
	}

	public static AppBuilder UseKeviFonts(this AppBuilder builder)
		=> builder.With(new FontManagerOptions
		{
			DefaultFamilyName = "resm:KyoshinEewViewer.Core.Assets.Fonts.NotoSansJP?assembly=KyoshinEewViewer.Core#Noto Sans JP",
			FontFallbacks = new[]
			{
				new FontFallback
				{
					FontFamily = new FontFamily("resm:KyoshinEewViewer.Core.Assets.Fonts.NotoSansJP?assembly=KyoshinEewViewer.Core#Noto Sans JP"),
				},
				new FontFallback
				{
					FontFamily = new FontFamily(Utils.IconFontName),
				},
			},
		});
}
