using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;

/// <summary>
/// EEW Client のメッセージを KEVi のモデルへ変換する
/// </summary>
public static class DmdataEewClientMessageConverter
{
	/// <summary>
	/// 地点予測の提供元識別子
	/// </summary>
	public const string PointForecastSourceKey = "dmdata-eewclient";

	/// <summary>
	/// 表示に使用する提供元名
	/// </summary>
	public const string DisplaySourceName = "EEW Client";

	/// <summary>
	/// EEW Client からは地点名が送られてこないため固定の名称を使用する
	/// </summary>
	public const string PointName = "EEW Client 予想地点";

	private static readonly TimeSpan JstOffset = TimeSpan.FromHours(9);

	/// <summary>
	/// eew メッセージを EEW モデルへ変換する
	/// </summary>
	/// <param name="message">type が eew のメッセージ</param>
	/// <param name="receiveTime">受信時刻</param>
	public static Models.Eew ToEew(DmdataEewClientMessage message, DateTime receiveTime)
	{
		if (string.IsNullOrWhiteSpace(message.EventId))
			throw new ArgumentException("eventId が存在しません", nameof(message));

		var regions = message.Regions ?? message.RegionForecasts;
		var accuracy = message.Accuracy;

		return new Models.Eew
		{
			Id = message.EventId,
			Source = EewSource.DmdataEewClient,
			DisplaySource = string.IsNullOrWhiteSpace(message.Author) ? DisplaySourceName : $"{DisplaySourceName}({message.Author})",
			IsCancelled = message.IsCanceled,
			IsTrueCancelled = message.IsCanceled,
			ReceiveTime = receiveTime,
			SerialNo = message.Serial,
			IsFinal = message.IsLastInfo,
			MaxIntensity = ParseIntensity(message.MaxInt?.From),
			IsIntensityOver = message.MaxInt?.To == "over",
			// 取消報は震源情報を持たない
			Hypocenter = message.IsCanceled ? null : new EewHypocenter
			{
				OccurrenceTime = ToJst(message.OriginTime ?? message.ArrivalTime) ?? throw new ArgumentException("originTime と arrivalTime のどちらも存在しません", nameof(message)),
				Place = message.EpicenterName,
				Location = ToLocation(message.EpicenterLocation),
				Magnitude = message.Magnitude is { } magnitude && double.IsFinite(magnitude) ? (float)magnitude : null,
				Depth = message.Depth ?? -1,
				// PLUM法のみ・レベル法の場合は仮定震源要素として扱う
				IsTemporary = message.IsPlumOnly || message.IsLevelOver,
				Accuracy = accuracy == null ? null : new EewHypocenterAccuracy
				{
					// 震源位置の精度が 9 の場合は最終報レベルの精度とみなす
					IsLocked = accuracy.Epicenters is { Length: >= 2 } ranks && ranks[1] == 9,
					LocationAccuracy = accuracy.Epicenters is { Length: >= 1 } epicenters ? epicenters[0] : 0,
					DepthAccuracy = accuracy.Depth ?? 0,
					MagnitudeAccuracy = accuracy.MagnitudeCalculation ?? 0,
				},
			},
			// 細分区域の予想震度は気象庁の発表ではなく EEW Client が計算した値
			CalculatedIntensityForecastMap = regions == null ? null : ToIntensityForecastMap(regions),
			CalculatedIntensityForecastSource = DisplaySourceName,
			// 細分区域ごとの警報有無は送られてこないため警報地域は扱わない
			WarningAreas = null,
			IsWarning = message.IsWarning,
		};
	}

	/// <summary>
	/// user-point メッセージから予想地点の座標を取り出す
	/// </summary>
	/// <returns>予想地点が未設定・共有されていない、または座標として解釈できない場合は null</returns>
	public static Location? ToUserPointLocation(DmdataEewClientMessage message)
	{
		if (message.Location is not { ValueKind: JsonValueKind.Array } location || location.GetArrayLength() < 2)
			return null;
		if (location[0] is not { ValueKind: JsonValueKind.Number } lon || location[1] is not { ValueKind: JsonValueKind.Number } lat)
			return null;
		return ToLocation([lon.GetDouble(), lat.GetDouble()]);
	}

	/// <summary>
	/// eew メッセージに含まれる予想地点の予測を地点予測へ変換する
	/// </summary>
	/// <param name="message">type が eew のメッセージ</param>
	/// <param name="receiveTime">受信時刻</param>
	/// <param name="userPointLocation">user-point で共有された予想地点の座標 共有されていない場合は null</param>
	/// <returns>予想地点が設定されていない、または予測震度が得られない場合は null</returns>
	public static EewPointForecast? ToPointForecast(DmdataEewClientMessage message, DateTime receiveTime, Location? userPointLocation)
	{
		if (string.IsNullOrWhiteSpace(message.EventId) || message.IsCanceled)
			return null;
		if (message.PointForecast is not { Intensity: { } intensity } pointForecast)
			return null;
		if (ToRealtimeIntensity(intensity) is not { } realtimeIntensity)
			return null;

		var sWave = pointForecast.SWave;
		return new EewPointForecast
		{
			EventId = message.EventId,
			SourceKey = PointForecastSourceKey,
			DisplaySource = intensity.Type == "plum" ? $"{DisplaySourceName} (PLUM)" : DisplaySourceName,
			SerialNo = message.Serial,
			PointName = PointName,
			// 座標が共有されていない場合は地図には表示しない
			Location = userPointLocation,
			RealtimeIntensity = realtimeIntensity,
			ArrivalTime = sWave?.Status == "ok" ? ToJst(sWave.Time) : null,
			OccurrenceTime = ToJst(message.OriginTime),
			ReceiveTime = receiveTime,
		};
	}

	/// <summary>
	/// 計測震度を求める 計測震度が無い場合は震度階級の下限値で代用する
	/// </summary>
	private static double? ToRealtimeIntensity(DmdataEewClientPointIntensity intensity)
	{
		if (intensity.K is { } k)
			return double.IsFinite(k) && k is >= -3 and <= 8 ? k : null;

		return ParseIntensity(intensity.Int) switch
		{
			JmaIntensity.Int0 => 0,
			JmaIntensity.Int1 => 0.5,
			JmaIntensity.Int2 => 1.5,
			JmaIntensity.Int3 => 2.5,
			JmaIntensity.Int4 => 3.5,
			JmaIntensity.Int5Lower => 4.5,
			JmaIntensity.Int5Upper => 5.0,
			JmaIntensity.Int6Lower => 5.5,
			JmaIntensity.Int6Upper => 6.0,
			JmaIntensity.Int7 => 6.5,
			_ => null,
		};
	}

	private static Dictionary<int, JmaIntensity> ToIntensityForecastMap(DmdataEewClientRegion[] regions)
	{
		var map = new Dictionary<int, JmaIntensity>();
		foreach (var region in regions)
		{
			var intensity = ParseIntensity(region.MaxInt ?? region.To);
			if (intensity == JmaIntensity.Unknown)
				continue;
			// 同一コードが重複した場合は大きい方を採用する
			if (!map.TryGetValue(region.Code, out var current) || current < intensity)
				map[region.Code] = intensity;
		}
		return map;
	}

	/// <summary>
	/// "5-" のような震度階級の文字列を解釈する 解釈できない場合は不明
	/// </summary>
	private static JmaIntensity ParseIntensity(string? value)
		=> string.IsNullOrWhiteSpace(value) || value is "over" or "不明" ? JmaIntensity.Unknown : value.ToJmaIntensity();

	/// <summary>
	/// [経度, 緯度] の配列を座標へ変換する
	/// </summary>
	private static Location? ToLocation(double[]? epicenterLocation)
	{
		if (epicenterLocation is not { Length: >= 2 })
			return null;
		var lon = epicenterLocation[0];
		var lat = epicenterLocation[1];
		if (!double.IsFinite(lat) || !double.IsFinite(lon) || lat is < -90 or > 90 || lon is < -180 or > 180)
			return null;
		return new Location((float)lat, (float)lon);
	}

	/// <summary>
	/// 日本標準時の DateTime へ変換する
	/// </summary>
	private static DateTime? ToJst(DateTimeOffset? value)
		=> value?.ToOffset(JstOffset).DateTime;
}
