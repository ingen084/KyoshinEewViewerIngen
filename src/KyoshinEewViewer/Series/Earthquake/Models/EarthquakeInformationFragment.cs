using KyoshinEewViewer.Core;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinMonitorLib;
using ReactiveUI;
using System;
using System.Linq;

namespace KyoshinEewViewer.Series.Earthquake.Models;

public abstract class EarthquakeInformationFragment : ReactiveObject
{
	// メモ　取り消しは上位でやる
	public static EarthquakeInformationFragment CreateFromJmxXmlDocument(string telegramId, JmaXmlDocument report)
	{
		switch (report.Control.Title)
		{
			case "震源に関する情報":
			case "顕著な地震の震源要素更新のお知らせ":
				{
					if (report.EarthquakeBody.Earthquake is not { } earthquake)
						throw new EarthquakeInformationFragmentProcessException("Earthquake がみつかりません");

					var depth = -1;
					Location? location = null;
					foreach (var c in earthquake.Hypocenter.Area.Coordinates)
					{
						// 度分 のときは深さだけ更新する
						if (c.Type == "震源位置（度分）")
						{
							depth = CoordinateConverter.GetDepth(c.Value) ?? depth;
							continue;
						}
						location = CoordinateConverter.GetLocation(c.Value);
						depth = CoordinateConverter.GetDepth(c.Value) ?? -1;
					}

					return new HypocenterInformationFragment
					{
						ArrivedTime = report.Head.ReportDateTime.DateTime,
						BasedTelegramId = telegramId,
						Title = report.Control.Title,
						IsTest = report.Control.Status == "試験",
						IsTraining = report.Control.Status == "訓練",

						OccurrenceTime = earthquake.OriginTime?.DateTime
							?? throw new EarthquakeInformationFragmentProcessException("OccurrenceTime がみつかりません"),
						Place = earthquake.Hypocenter.Area.Name,
						Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? m
							: throw new EarthquakeInformationFragmentProcessException("Magnitude がfloatにパースできません"),
						MagnitudeAlternativeText = float.IsNaN(m) ? earthquake.Magnitude.Description : null,
						Depth = depth,
						Location = location
							?? throw new EarthquakeInformationFragmentProcessException("Location がみつかりません"),

						Comment = report.EarthquakeBody.Comments?.ForecastCommentText,
						FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
					};
				}
			case "震度速報":
				{
					if (report.EarthquakeBody.Intensity?.Observation is not { } observation)
						throw new EarthquakeWatchException("Observation がみつかりません");

					string? areaName = null;
					var isOnlyPosition = true;
					foreach (var pref in observation.Prefs)
					{
						// すでに複数件存在することが判明していれば戻る
						if (!isOnlyPosition)
							break;
						foreach (var area in pref.Areas)
						{
							// すでに area の取得ができていれば複数箇所存在するフラグを立てる
							if (areaName != null && isOnlyPosition)
							{
								isOnlyPosition = false;
								break;
							}
							// 未取得であれば area に代入
							areaName = area.Name;
						}
					}

					return new IntensityInformationFragment
					{
						ArrivedTime = report.Head.ReportDateTime.DateTime,
						BasedTelegramId = telegramId,
						Title = report.Control.Title,
						IsTest = report.Control.Status == "試験",
						IsTraining = report.Control.Status == "訓練",

						Place = areaName
							?? throw new EarthquakeInformationFragmentProcessException("Place がみつかりません"),
						DetectionTime = report.Head.TargetDateTime?.DateTime
							?? throw new EarthquakeInformationFragmentProcessException("TargetDateTime がみつかりません"),
						MaxIntensity = observation.MaxInt?.ToJmaIntensity()
							?? throw new EarthquakeInformationFragmentProcessException("MaxIntensity がみつかりません"),
						IsOnlypoint = isOnlyPosition,
						Comment = report.EarthquakeBody.Comments?.ForecastCommentText,
						FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
					};
				}
			case "震源・震度に関する情報":
				{
					if (report.EarthquakeBody.Earthquake is not { } earthquake)
						throw new EarthquakeInformationFragmentProcessException("Earthquake がみつかりません");

					var depth = -1;
					Location? location = null;
					foreach (var c in earthquake.Hypocenter.Area.Coordinates)
					{
						// 度分 のときは深さだけ更新する
						if (c.Type == "震源位置（度分）")
						{
							depth = CoordinateConverter.GetDepth(c.Value) ?? depth;
							continue;
						}
						location = CoordinateConverter.GetLocation(c.Value);
						depth = CoordinateConverter.GetDepth(c.Value) ?? -1;
					}

					return new HypocenterAndIntensityInformationFragment
					{
						ArrivedTime = report.Head.ReportDateTime.DateTime,
						BasedTelegramId = telegramId,
						Title = report.Control.Title,
						IsTest = report.Control.Status == "試験",
						IsTraining = report.Control.Status == "訓練",

						OccurrenceTime = earthquake.OriginTime?.DateTime
							?? throw new EarthquakeInformationFragmentProcessException("OccurrenceTime がみつかりません"),
						Place = earthquake.Hypocenter.Area.Name,
						Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? m
							: throw new EarthquakeInformationFragmentProcessException("Magnitude がfloatにパースできません"),
						MagnitudeAlternativeText = float.IsNaN(m) ? earthquake.Magnitude.Description : null,
						Depth = depth,
						Location = location
							?? throw new EarthquakeInformationFragmentProcessException("Location がみつかりません"),

						MaxIntensity = report.EarthquakeBody.Intensity?.Observation?.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
						IsForeign = report.Head.Title == "遠地地震に関する情報",

						Comment = report.EarthquakeBody.Comments?.ForecastCommentText,
						FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
					};
				}
			case "長周期地震動に関する観測情報":
				{
					if (report.EarthquakeBody.Earthquake is not { } earthquake)
						throw new EarthquakeInformationFragmentProcessException("Earthquake がみつかりません");

					var depth = -1;
					Location? location = null;
					foreach (var c in earthquake.Hypocenter.Area.Coordinates)
					{
						// 度分 のときは深さだけ更新する
						if (c.Type == "震源位置（度分）")
						{
							depth = CoordinateConverter.GetDepth(c.Value) ?? depth;
							continue;
						}
						location = CoordinateConverter.GetLocation(c.Value);
						depth = CoordinateConverter.GetDepth(c.Value) ?? -1;
					}

					return new LpgmIntensityInformationFragment
					{
						ArrivedTime = report.Head.ReportDateTime.DateTime,
						BasedTelegramId = telegramId,
						Title = report.Control.Title,
						IsTest = report.Control.Status == "試験",
						IsTraining = report.Control.Status == "訓練",

						OccurrenceTime = earthquake.OriginTime?.DateTime
							?? throw new EarthquakeInformationFragmentProcessException("OccurrenceTime がみつかりません"),
						Place = earthquake.Hypocenter.Area.Name,
						Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? m
							: throw new EarthquakeInformationFragmentProcessException("Magnitude がfloatにパースできません"),
						MagnitudeAlternativeText = float.IsNaN(m) ? earthquake.Magnitude.Description : null,
						Depth = depth,
						Location = location
							?? throw new EarthquakeInformationFragmentProcessException("Location がみつかりません"),

						MaxIntensity = report.EarthquakeBody.Intensity?.Observation?.MaxInt?.ToJmaIntensity() ?? JmaIntensity.Unknown,
						MaxLpgmIntensity = report.EarthquakeBody.Intensity?.Observation?.MaxLgInt?.ToLpgmIntensity() ?? LpgmIntensity.Unknown,
						IsForeign = false,

						Comment = report.EarthquakeBody.Comments?.ForecastCommentText,
						FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
					};
				}
			default:
				throw new EarthquakeInformationFragmentProcessException($"不明な電文タイトルです: {report.Control.Title}");
		}

	}

	public static (string EventId, EarthquakeInformationFragment Fragment)[] CreateFromTsunamiJmxXmlDocument(string telegramId, JmaXmlDocument report)
	{
		if (report.Control.Title != "津波警報・注意報・予報a")
			throw new EarthquakeInformationFragmentProcessException($"不明な電文タイトルです: {report.Control.Title}");

		// イベントIDごとに分割する
		var eventIds = report.Head.EventId.Split(' ');
		var earthquakes = report.TsunamiBody.Earthquakes.ToArray();
		if (earthquakes.Length != eventIds.Length)
			throw new EarthquakeInformationFragmentProcessException($"eventId の数と earthquake タグの数が一致しません。 eventId: {eventIds.Length} earthquake: {report.TsunamiBody.Earthquakes.Count()}");

		var result = new (string EventId, EarthquakeInformationFragment Fragment)[eventIds.Length];

		for (var i = 0; i < eventIds.Length; i++)
		{
			var earthquake = earthquakes[i];

			var depth = -1;
			Location? location = null;
			foreach (var c in earthquake.Hypocenter.Area.Coordinates)
			{
				// 度分 のときは深さだけ更新する
				if (c.Type == "震源位置（度分）")
				{
					depth = CoordinateConverter.GetDepth(c.Value) ?? depth;
					continue;
				}
				location = CoordinateConverter.GetLocation(c.Value);
				depth = CoordinateConverter.GetDepth(c.Value) ?? -1;
			}

			result[i] = (eventIds[i], new HypocenterInformationFragment
			{
				ArrivedTime = report.Head.ReportDateTime.DateTime,
				BasedTelegramId = telegramId,
				Title = report.Control.Title,
				IsTest = report.Control.Status == "試験",
				IsTraining = report.Control.Status == "訓練",

				OccurrenceTime = earthquake.OriginTime?.DateTime
							?? throw new EarthquakeInformationFragmentProcessException("OccurrenceTime がみつかりません"),
				Place = earthquake.Hypocenter.Area.Name,
				Location = location
							?? throw new EarthquakeInformationFragmentProcessException("Location がみつかりません"),
				Magnitude = earthquake.Magnitude.TryGetFloatValue(out var m) ? m
							: throw new EarthquakeInformationFragmentProcessException("Magnitude がfloatにパースできません"),
				MagnitudeAlternativeText = float.IsNaN(m) ? earthquake.Magnitude.Description : null,
				Depth = depth,

				Comment = report.EarthquakeBody.Comments?.ForecastCommentText,
				FreeFormComment = report.EarthquakeBody.Comments?.FreeFormComment,
			});
		}
		return result;
	}

	/// <summary>
	/// 発表時刻
	/// </summary>
	public required DateTime ArrivedTime { get; init; }

	/// <summary>
	/// ベースとなった電文ID
	/// </summary>
	public required string BasedTelegramId { get; init; }

	/// <summary>
	/// 電文名
	/// </summary>
	public required string Title { get; set; }

	/// <summary>
	/// 訓練
	/// </summary>
	public required bool IsTraining { get; init; }

	/// <summary>
	/// 試験
	/// </summary>
	public required bool IsTest { get; init; }

	private bool _isCancelled;
	/// <summary>
	/// 情報が取り消されたか
	/// </summary>
	public bool IsCancelled
	{
		get => _isCancelled;
		set => this.RaiseAndSetIfChanged(ref _isCancelled, value);
	}

	private bool _isCorrected;
	/// <summary>
	/// 情報が訂正済みか
	/// </summary>
	public bool IsCorrected
	{
		get => _isCorrected;
		set => this.RaiseAndSetIfChanged(ref _isCorrected, value);
	}
}

/// <summary>
/// 震源情報･顕著な地震の震源要素更新のお知らせ
/// </summary>
public class HypocenterInformationFragment : EarthquakeInformationFragment
{
	/// <summary>
	/// 発生時刻
	/// </summary>
	public required DateTime OccurrenceTime { get; init; }

	/// <summary>
	/// 震央
	/// </summary>
	public required string Place { get; init; }

	/// <summary>
	/// 震央座標
	/// </summary>
	public required Location Location { get; init; }

	/// <summary>
	/// マグニチュード
	/// </summary>
	public required float Magnitude { get; init; }

	/// <summary>
	/// マグニチュードの代替テキスト
	/// </summary>
	public required string? MagnitudeAlternativeText { get; init; }

	/// <summary>
	/// 深さ(km)
	/// </summary>
	public required int Depth { get; init; }

	/// <summary>
	/// 固定付加文
	/// </summary>
	public string? Comment { get; init; }

	/// <summary>
	/// 自由形式文
	/// </summary>
	public string? FreeFormComment { get; init; }
}

/// <summary>
/// 震源･震度情報
/// </summary>
public class HypocenterAndIntensityInformationFragment : HypocenterInformationFragment
{
	/// <summary>
	/// 最大震度
	/// </summary>
	public required JmaIntensity MaxIntensity { get; init; }

	/// <summary>
	/// 海外で発生した地震か
	/// </summary>
	public required bool IsForeign { get; init; }
}

/// <summary>
/// 震度速報
/// </summary>
public class IntensityInformationFragment : EarthquakeInformationFragment
{
	/// <summary>
	/// 代表地域
	/// </summary>
	public required string Place { get; init; }

	/// <summary>
	/// 検知時刻
	/// </summary>
	public required DateTime DetectionTime { get; init; }

	/// <summary>
	/// 最大震度
	/// </summary>
	public required JmaIntensity MaxIntensity { get; init; }

	/// <summary>
	/// 発表地域が1つのみか
	/// </summary>
	public bool IsOnlypoint { get; init; }

	/// <summary>
	/// 固定付加文
	/// </summary>
	public string? Comment { get; init; }

	/// <summary>
	/// 自由形式文
	/// </summary>
	public string? FreeFormComment { get; init; }
}

/// <summary>
/// 長周期
/// </summary>
public class LpgmIntensityInformationFragment : HypocenterAndIntensityInformationFragment
{
	/// <summary>
	/// 最大の長周期地震動階級
	/// </summary>
	public required LpgmIntensity MaxLpgmIntensity { get; init; }
}

/// <summary>
/// 推計震度分布
/// </summary>
//public class EstimatedIntensityDistributionInformationFragment : EarthquakeInformationFragment
//{
//	// TODO: 未実装
//}
