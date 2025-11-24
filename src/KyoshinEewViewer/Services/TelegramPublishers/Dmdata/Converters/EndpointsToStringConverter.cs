using Avalonia.Data.Converters;
using DmdataSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata.Converters;

/// <summary>
/// エンドポイント配列を改行区切り文字列に変換するコンバーター
/// </summary>
public class EndpointsToStringConverter : IValueConverter
{
	private static readonly Dictionary<string, string> EndpointNames = new()
	{
		{ DmdataV2SocketEndpoints.Global, "グローバル" },
		{ DmdataV2SocketEndpoints.Tokyo, "東京" },
		{ DmdataV2SocketEndpoints.Osaka, "大阪" },
		{ DmdataV2SocketEndpoints.Apne1Az4, "apne1-az4" },
		{ DmdataV2SocketEndpoints.Apne1Az1, "apne1-az1" },
		{ DmdataV2SocketEndpoints.Apne3Az3, "apne3-az3" },
		{ DmdataV2SocketEndpoints.Apne3Az1, "apne3-az1" },
	};

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is not string[] endpoints)
			return "なし";

		if (endpoints.Length == 0)
			return "なし";

		var displayNames = endpoints.Select(endpoint =>
			EndpointNames.TryGetValue(endpoint, out var name) ? name : endpoint
		);

		return string.Join(", ", displayNames);
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotImplementedException();
}
