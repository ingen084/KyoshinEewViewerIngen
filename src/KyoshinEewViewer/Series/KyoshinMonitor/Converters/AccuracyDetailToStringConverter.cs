using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Converters;

public class AccuracyDetailToStringConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> parameter switch
		{
			"epicenter" or "depth" => value switch
			{
				0 => "不明",
				1 => "P/S波レベル超え or IPF(1点) [気象庁]",
				2 => "IPF法(2点) [気象庁]",
				3 => "IPF法(3点/4点) [気象庁]",
				4 => "IPF法(5点以上) [気象庁]",
				5 => "Hi-net(4点以下or情報なし) [防災科研]",
				6 => "Hi-net(5点以上) [防災科研]",
				7 => "EPOS(海域[観測網外])",
				8 => "EPOS(内陸[観測網内])",
				_ => $"不明({value})",
			},
			"magnitude" => value switch
			{
				0 => "不明",
				2 => "Hi-net [防災科研]",
				3 => "全点P相",
				4 => "P相/全相混在",
				5 => "全点全相",
				6 => "EPOS",
				8 => "P/S波レベル超え",
				_ => $"不明({value})",
			},
			_ => "パラメータが設定されていません",
		};

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}
