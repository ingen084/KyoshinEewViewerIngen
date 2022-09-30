using Avalonia.Controls;
using DynamicData;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.JmaXmlParser.Data.Meteorological;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Series.Typhoon.Models;
using KyoshinEewViewer.Services;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Location = KyoshinMonitorLib.Location;

namespace KyoshinEewViewer.Series.Typhoon.Services;
public class TyphoonWatchService : ReactiveObject
{
	private bool _enable;
	public bool Enabled
	{
		get => _enable;
		private set => this.RaiseAndSetIfChanged(ref _enable, value);
	}

	private ILogger Logger { get; }
	private TelegramProvideService TelegramProvideService { get; }

	private Regex TelegramTypeId { get; } = new("VPTW6(\\d)", RegexOptions.Compiled);

	public TyphoonWatchService(TelegramProvideService telegramProvideService)
	{
		Logger = LoggingService.CreateLogger(this);
		TelegramProvideService = telegramProvideService;

		if (Design.IsDesignMode)
			return;

		TelegramProvideService.Subscribe(
			InformationCategory.Typhoon,
			async (s, telegrams) =>
			{
				Typhoons.Clear();
				foreach (var t in telegrams)
				{
					try
					{
						if (t.Title != "台風解析・予報情報（５日予報）（Ｈ３０）")
							continue;
						Logger.LogInformation("台風情報処理中: {key}", t.Key);
						var match = TelegramTypeId.Match(t.RawId);
						AggregateTyphoon(ProcessXml(await t.GetBodyAsync(), match.ToString()));
					}
					catch (Exception ex)
					{
						Logger.LogError(ex, "台風情報初期電文取得中に例外が発生しました");
					}
				}
				// 消滅した台風は1日経過で削除
				Typhoons.RemoveMany(Typhoons.Where(t => t.IsEliminated && t.Current.TargetDateTime.AddDays(1) < TimerService.Default.CurrentTime).ToArray());
				Enabled = true;
			},
			async t =>
			{
				var sw = Stopwatch.StartNew();
				// 受信した
				try
				{
					Logger.LogInformation("台風情報を受信しました");
					var match = TelegramTypeId.Match(t.RawId);
					AggregateTyphoon(ProcessXml(await t.GetBodyAsync(), match.ToString()));
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "台風電文処理中に例外が発生しました");
				}
				finally
				{
					Logger.LogDebug("台風情報処理時間: {time}ms", sw.Elapsed.TotalMilliseconds.ToString("0.000"));
				}
			},
			s => Enabled = !s.isAllFailed);
	}

	public List<TyphoonItem> Typhoons { get; } = new();

	public event Action<TyphoonItem>? TyphoonUpdated;

	private void AggregateTyphoon(TyphoonItem? typhoon)
	{
		if (typhoon == null)
			return;

		if (Typhoons.FirstOrDefault(t => t.Id == typhoon.Id) is not TyphoonItem previousItem)
		{
			// 過去のデータが存在しない場合はそのまま代入
			Typhoons.Add(typhoon);
			if (Enabled)
				TyphoonUpdated?.Invoke(typhoon);
			return;
		}

		// キャッシュされている情報のほうが新しい場合無視
		if (previousItem.Current.TargetDateTime >= typhoon.Current.TargetDateTime)
			return;

		// 過去の中心位置の情報が存在する場合はリストに追加していく
		var current = new[] { typhoon.Current.Center };
		typhoon.LocationHistory = previousItem.LocationHistory?.Concat(current).ToArray() ?? current;
		// 消滅報でないかつ現在の情報に過去の予報円が存在しない場合、予報円の情報を引き継ぐ
		if (
			(previousItem.ForecastPlaces?.Any() ?? false) &&
			!(typhoon.ForecastPlaces?.Any() ?? false) && !typhoon.IsEliminated)
			typhoon.ForecastPlaces = previousItem.ForecastPlaces;

		// 置き換え
		Typhoons.Replace(previousItem, typhoon);
		if (Enabled)
			TyphoonUpdated?.Invoke(typhoon);
	}

	// 受け取った stream はこの中でdisposeします ちゅうい
	public TyphoonItem? ProcessXml(Stream body, string telegramId)
	{
		using (body)
		{
			using var document = new JmaXmlDocument(body);

			// 今のところこれのみ対応
			if (document.Control.Title != "台風解析・予報情報（５日予報）（Ｈ３０）")
				return null;

			var forecastPlaces = new List<TyphoonPlace>();
			(string, bool, TyphoonPlace?) currentPlace = ("", false, null);
			TyphoonPlace? estimatePlace = null;

			(Direction d, int l)? ParseAxis(Axis axis)
			{
				var direction = CoordinateConverter.GetDirection(axis.Direction.Value) ?? Direction.None;
				// サイズが取得できないときはデータがないという扱いにする
				var re = axis.Radiuses.Where(r => r.Unit == "km");
				if (!re.Any() || string.IsNullOrEmpty(re.First().Value))
					return null;
				if (!int.TryParse(re.First().Value, out var radius))
					throw new Exception("予報円のサイズが取得できませんでした");
				return (direction, radius);
			}

			TyphoonRenderCircle? ParseCircleElement(Location center, TyphoonCircle circle)
			{
				// 取得できる方向要素を取得
				var axes = circle.Axes.Select(ParseAxis).Where(x => x is not null).Select(x => x!.Value).ToArray();
				// 取得できなければnull
				if (axes.Length <= 0)
					return null;

				var range = axes[0].l;
				// 中心からの移動量
				var moveLength = new PointD();
				if (axes[0].d != Direction.None)
				{
					range = (axes[0].l + axes[1].l) / 2;
					moveLength = ((axes[0].d.GetVector() * axes[0].l) + (axes[1].d.GetVector() * axes[1].l)) / 2;
				}

				var rawCenter = center.MoveTo(moveLength.Direction + 90, moveLength.Length * 1000);
				return new TyphoonRenderCircle(center, range, rawCenter);
			}

			(string, bool, TyphoonPlace) ProcessNowTyphoonCircle(MeteorologicalInfo info)
			{
				var namePart = info.MeteorologicalInfoKindProperties.First(p => p.Type == "呼称");
				var number = string.IsNullOrWhiteSpace(namePart.TyphoonNamePart.Number) ? null : namePart.TyphoonNamePart.Number[2..];

				return (
					number != null ? $"台風{number}号" : ("熱帯低気圧" + (telegramId.StartsWith("VPTW6") ? new string((char)('a' + (telegramId.Last() - '0')), 1) : "")),
					namePart.TyphoonNamePart.Remark?.Contains("消滅") ?? false,
					ProcessEstimateTyphoonCircle(info)
				);
			}

			TyphoonPlace ProcessEstimateTyphoonCircle(MeteorologicalInfo info)
			{
				TyphoonRenderCircle? strongCircle = null, stormCircle = null;

				var classPart = info.MeteorologicalInfoKindProperties.First(p => p.Type == "階級");

				var centerPart = info.MeteorologicalInfoKindProperties.First(p => p.Type == "中心");
				var center = CoordinateConverter.GetLocation(centerPart.CenterPart.Coordinates.First(c => c.Type == "中心位置（度）").Value) ?? throw new Exception("現在の中心座標が取得できません");

				// 消滅時などの場合は存在しない
				MeteorologicalInfoKindProperty? windPart = null;
				if (info.MeteorologicalInfoKindProperties.Any(p => p.Type == "風"))
				{
					windPart = info.MeteorologicalInfoKindProperties.First(p => p.Type == "風");
					strongCircle = ParseCircleElement(center, windPart.Value.WarningAreaParts.First(a => a.Type == "強風域").Circle);
					stormCircle = ParseCircleElement(center, windPart.Value.WarningAreaParts.First(a => a.Type == "暴風域").Circle);
				}

				return new(
					string.IsNullOrEmpty(classPart.ClassPart.AreaClass) ? "―" : classPart.ClassPart.AreaClass,
					string.IsNullOrEmpty(classPart.ClassPart.IntensityClass) ? "―" : classPart.ClassPart.IntensityClass,
					info.DateTime.DateTime,
					info.DateTimeType,
					centerPart.CenterPart.Location ?? "-",
					centerPart.CenterPart.Pressure.TryGetIntValue(out var centralPressure) ? centralPressure : -1,
					windPart?.WindPart.WindSpeeds.First(s => s.Unit == "m/s" && s.Type == "最大風速").TryGetIntValue(out var c) ?? false ? c : null,
					windPart?.WindPart.WindSpeeds.First(s => s.Unit == "m/s" && s.Type == "最大風速").Condition == "中心付近",
					windPart?.WindPart.WindSpeeds.First(s => s.Unit == "m/s" && s.Type == "最大瞬間風速").TryGetIntValue(out var m) ?? false ? m : null,
					center,
					strongCircle,
					stormCircle
				);
			}

			// 引数として MeteorologicalInfo をとる
			TyphoonPlace ProcessForecastTyphoonCircle(MeteorologicalInfo info)
			{
				TyphoonRenderCircle? forecastCircle = null, forecastStormCircle = null;

				var classPart = info.MeteorologicalInfoKindProperties.First(p => p.Type == "階級");

				var centerPart = info.MeteorologicalInfoKindProperties.First(p => p.Type == "中心");
				var area = info.TyphoonCircles.First(c => c.Type == "予報円");
				var center = CoordinateConverter.GetLocation(area.BasePoints.First(p => p.Type == "中心位置（度）").Value) ?? throw new Exception("中心座標が取得できませんでした");
				forecastCircle = ParseCircleElement(center, area);

				// 消滅時などの場合は存在しない
				MeteorologicalInfoKindProperty? windPart = null;
				if (info.MeteorologicalInfoKindProperties.Any(p => p.Type == "風"))
				{
					windPart = info.MeteorologicalInfoKindProperties.First(p => p.Type == "風");
					forecastStormCircle = ParseCircleElement(center, windPart.Value.WarningAreaParts.First(a => a.Type == "暴風警戒域").Circle);
				}

				return new(
					string.IsNullOrEmpty(classPart.ClassPart.AreaClass) ? "―" : classPart.ClassPart.AreaClass,
					string.IsNullOrEmpty(classPart.ClassPart.IntensityClass) ? "―" : classPart.ClassPart.IntensityClass,
					info.DateTime.DateTime,
					info.DateTimeType,
					centerPart.CenterPart.Location ?? "―",
					centerPart.CenterPart.Pressure.TryGetIntValue(out var centralPressure) ? centralPressure : -1,
					windPart?.WindPart.WindSpeeds.First(s => s.Unit == "m/s" && s.Type == "最大風速").TryGetIntValue(out var c) ?? false ? c : null,
					windPart?.WindPart.WindSpeeds.First(s => s.Unit == "m/s" && s.Type == "最大風速").Condition == "中心付近",
					windPart?.WindPart.WindSpeeds.First(s => s.Unit == "m/s" && s.Type == "最大瞬間風速").TryGetIntValue(out var m) ?? false ? m : null,
					center,
					forecastCircle,
					forecastStormCircle
				);
			}

			foreach (var info in document.MeteorologicalBody.MeteorologicalInfos)
			{
				// 現況
				if (info.DateTimeType == "実況")
				{
					currentPlace = ProcessNowTyphoonCircle(info);
					continue;
				}
				// 推定
				if (info.DateTimeType?.StartsWith("推定") ?? false)
				{
					estimatePlace = ProcessEstimateTyphoonCircle(info);
					continue;
				}

				// 予報
				forecastPlaces.Add(ProcessForecastTyphoonCircle(info));
			}

			if (currentPlace.Item3 == null)
				throw new Exception("台風の実況情報が存在しません");

			return new TyphoonItem(document.Head.EventId, currentPlace.Item1, currentPlace.Item2, currentPlace.Item3, estimatePlace) { ForecastPlaces = forecastPlaces.ToArray() };
		}
	}
}
