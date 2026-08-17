using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinMonitorLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

public abstract partial class EarthquakeInformationHost(bool isReplay, KyoshinEewViewerConfiguration config) : ObservableObject
{
	public event Action<DateTime, Eew[]>? EewUpdated;
	protected void OnEewUpdated(DateTime time, Eew[] eews) => EewUpdated?.Invoke(time, eews);

	public event Action<(DateTime time, RealtimeObservationPoint[] data, KyoshinEvent[] events)>? RealtimeDataUpdated;
	protected void OnRealtimeDataUpdated((DateTime time, RealtimeObservationPoint[] data, KyoshinEvent[] events) data) => RealtimeDataUpdated?.Invoke(data);

	public event Action<(DateTime time, KyoshinEvent e, bool isLevelUp, bool isRegionExpanded, bool isSubRegionExpanded)>? KyoshinEventUpdated;
	protected void OnKyoshinEventUpdated((DateTime time, KyoshinEvent e, bool isLevelUp, bool isRegionExpanded, bool isSubRegionExpanded) data) => KyoshinEventUpdated?.Invoke(data);

	protected KyoshinEewViewerConfiguration Config { get; } = config;

	/// <summary>
	/// Region → SubRegion[] のマッピング（全観測点から構築）
	/// </summary>
	public Dictionary<string, HashSet<string?>> RegionSubRegionMap { get; } = [];

	public abstract DateTime CurrentTime { get; }

	public MapData? MapData { get; set; }

	public bool IsReplay { get; } = isReplay;

	[ObservableProperty]
	public partial string ReplayDescription { get; protected set; } = "";

	/// <summary>
	/// マップ表示位置のリクエスト
	/// </summary>
	[ObservableProperty]
	public partial MapNavigationRequest? MapNavigationRequest { get; protected set; }

	/// <summary>
	/// マップ表示用のパラメータ
	/// </summary>
	[ObservableProperty]
	public partial MapDisplayParameter MapDisplayParameter { get; protected set; }

	[ObservableProperty]
	public partial bool IsWorking { get; set; }

	[ObservableProperty]
	public partial DateTime CurrentDisplayTime { get; set; } = DateTime.Now;

	[ObservableProperty]
	public partial bool IsSignalNowEewReceiving { get; set; }

	[ObservableProperty]
	public partial bool DmdataReceiving { get; set; }

	[ObservableProperty]
	public partial bool DmdataWarningOnlyReceiving { get; set; }

	[ObservableProperty]
	public partial bool DmdataDisconnected { get; set; }

	[ObservableProperty]
	public partial bool AxisReceiving { get; set; }

	[ObservableProperty]
	public partial bool AxisDisconnected { get; set; }

	[ObservableProperty]
	public partial bool AllEewSourceFailed { get; set; }

	/// <summary>
	/// 警告メッセージ
	/// </summary>
	[ObservableProperty]
	public partial string? WarningMessage { get; set; }

	[ObservableProperty]
	public partial bool ShowIntensityColorSample { get; set; }

	[ObservableProperty]
	public partial Eew[] Eews { get; set; } = [];

	[ObservableProperty]
	public partial KyoshinEvent[] KyoshinEvents { get; set; } = [];

	/// <summary>
	/// 揺れ検知地域
	/// </summary>
	[ObservableProperty]
	public partial ShakeDetectedRegion[] ShakeDetectedRegions { get; set; } = [];

	/// <summary>
	/// 揺れ検知の最高レベル
	/// </summary>
	[ObservableProperty]
	public partial KyoshinEventLevel ShakeDetectedLevel { get; set; }

	/// <summary>
	/// 揺れ検知パネルを表示するかどうか
	/// 通知レベル未満の場合は非表示
	/// </summary>
	[ObservableProperty]
	public partial bool ShowShakeDetectedPanel { get; set; }

	protected void UpateFocusPoint(DateTime time)
	{
		// 震度が不明でない、キャンセルされてない、最終報から1分未満、座標が設定されている場合のみズーム
		var targetEews = Eews.Where(e => /*(e.Source == EewSource.SignalNowProfessional && e.Intensity != JmaIntensity.Unknown) &&*/ !e.IsCancelled && (!e.IsFinal || (time - e.ReceiveTime).Minutes < 1) && e.Hypocenter?.Location != null).ToArray();

		// 震源座標が未受信のEEWでも、震度1以上の地点予測があればズームの対象にする
		var targetPointForecasts = Eews
			.Where(e => !e.IsCancelled && (!e.IsFinal || (time - e.ReceiveTime).Minutes < 1))
			.SelectMany(e => e.PointForecasts ?? [])
			.Where(f => f.Location != null && f.Intensity >= JmaIntensity.Int1)
			.ToArray();

		if (targetEews.Length <= 0 && targetPointForecasts.Length <= 0 && !KyoshinEvents.Any(k => k.Level >= Config.KyoshinMonitor.EventNotificationLevel))
		{
			MapNavigationRequest = null;
			return;
		}

		// 自動ズーム範囲を計算
		var minLat = float.MaxValue;
		var maxLat = float.MinValue;
		var minLng = float.MaxValue;
		var maxLng = float.MinValue;
		void CheckLocation(Location p)
		{
			if (minLat > p.Latitude)
				minLat = p.Latitude;
			if (minLng > p.Longitude)
				minLng = p.Longitude;

			if (maxLat < p.Latitude)
				maxLat = p.Latitude;
			if (maxLng < p.Longitude)
				maxLng = p.Longitude;
		}

		// 必須範囲
		var minLat2 = float.MaxValue;
		var maxLat2 = float.MinValue;
		var minLng2 = float.MaxValue;
		var maxLng2 = float.MinValue;
		void CheckLocation2(Location p)
		{
			if (minLat2 > p.Latitude)
				minLat2 = p.Latitude;
			if (minLng2 > p.Longitude)
				minLng2 = p.Longitude;

			if (maxLat2 < p.Latitude)
				maxLat2 = p.Latitude;
			if (maxLng2 < p.Longitude)
				maxLng2 = p.Longitude;
		}

		// EEW
		foreach (var e in targetEews)
		{
			var l = e.Hypocenter?.Location;
			var sizeP = new PointD(.1, .1);
			var size = 1;
			if (Config.KyoshinMonitor.ReceiveMode == KyoshinEewViewerConfiguration.KyoshinMonitorConfig.Mode.None)
				size = 2;

			CheckLocation2(l!);
			CheckLocation(new(l!.Latitude - size, l.Longitude - size));
			CheckLocation(new(l.Latitude + size, l.Longitude + size));

			// 各地域の範囲
			if (MapData?.TryGetLayer(LandLayerType.EarthquakeInformationSubdivisionArea, out var layer) ?? false)
			{
				if (Config.Eew.FillForecastIntensity && e.IntensityForecastMap != null)
				{
					foreach (var a in e.IntensityForecastMap)
					{
						foreach (var p in layer.FindPolygon(a.Key))
						{
							CheckLocation((p.BoundingBox.TopLeft - sizeP).CastLocation());
							CheckLocation((p.BoundingBox.BottomRight + sizeP).CastLocation());

							CheckLocation2(p.BoundingBox.TopLeft.CastLocation());
							CheckLocation2(p.BoundingBox.BottomRight.CastLocation());
						}
					}
				}
				else if (Config.Eew.FillWarningArea && e.WarningAreas != null)
				{
					foreach (var a in e.WarningAreas.Codes)
					{
						foreach (var p in layer.FindPolygon(a))
						{
							CheckLocation((p.BoundingBox.TopLeft - sizeP).CastLocation());
							CheckLocation((p.BoundingBox.BottomRight + sizeP).CastLocation());

							CheckLocation2(p.BoundingBox.TopLeft.CastLocation());
							CheckLocation2(p.BoundingBox.BottomRight.CastLocation());
						}
					}
				}
			}
		}

		// 地点予測 点であるためEEWの震源より小さい余白にする
		foreach (var f in targetPointForecasts)
		{
			var l = f.Location!;
			CheckLocation2(l);
			CheckLocation(new(l.Latitude - .3f, l.Longitude - .3f));
			CheckLocation(new(l.Latitude + .3f, l.Longitude + .3f));
		}

		// Event
		foreach (var e in KyoshinEvents.Where(k => k.Level >= Config.KyoshinMonitor.EventNotificationLevel))
		{
			CheckLocation2(e.TopLeft);
			CheckLocation2(e.BottomRight);
			CheckLocation(new(e.TopLeft.Latitude - .5f, e.TopLeft.Longitude - .5f));
			CheckLocation(new(e.BottomRight.Latitude + .5f, e.BottomRight.Longitude + .5f));
		}

		// EEW によるズームが行われるときのみ左側の領域確保を行う
		// MapPadding = targetEews.Any() ? new Thickness(310, 0, 0, 0) : new Thickness(0);

		// 初回移動時は MustBound を設定しないようにしてズームを適切に動作させるようにする
		MapNavigationRequest = new(
			new(minLat, minLng, maxLat - minLat, maxLng - minLng),
			MapNavigationRequest != null ? new(minLat2, minLng2, maxLat2 - minLat2, maxLng2 - minLng2) : null
		);
	}
}
