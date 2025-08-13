using Avalonia.Data.Converters;
using DmdataSharp.Redundancy;
using System;
using System.Globalization;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata.Converters;

/// <summary>
/// RedundancyStatusを日本語文字列に変換するコンバーター
/// </summary>
public class RedundancyStatusToStringConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not RedundancyStatus status)
			return "不明";

		return status switch
		{
			RedundancyStatus.FullyConnected => "完全接続",
			RedundancyStatus.PartiallyConnected => "部分接続",
			RedundancyStatus.Disconnected => "未接続",
			_ => status.ToString(),
		};
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}