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

public abstract class EarthquakeInformationHost(bool isReplay, KyoshinEewViewerConfiguration config) : ObservableObject
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

	private string _replayDescription = "";
	public string ReplayDescription
	{
		get => _replayDescription;
		protected set => SetProperty(ref _replayDescription, value);
	}

	private MapNavigationRequest? _mapNavigationRequest;
	/// <summary>
	/// マップ表示位置のリクエスト
	/// </summary>
	public MapNavigationRequest? MapNavigationRequest
	{
		get => _mapNavigationRequest;
		protected set => SetProperty(ref _mapNavigationRequest, value);
	}

	private MapDisplayParameter _mapDisplayParameter;
	/// <summary>
	/// マップ表示用のパラメータ
	/// </summary>
	public MapDisplayParameter MapDisplayParameter
	{
		get => _mapDisplayParameter;
		protected set => SetProperty(ref _mapDisplayParameter, value);
	}

	private bool _isWorking;
	public bool IsWorking
	{
		get => _isWorking;
		set => SetProperty(ref _isWorking, value);
	}

	private DateTime _currentDisplayTime = DateTime.Now;
	public DateTime CurrentDisplayTime
	{
		get => _currentDisplayTime;
		set => SetProperty(ref _currentDisplayTime, value);
	}

	private bool _isSignalNowEewReceiving;
	public bool IsSignalNowEewReceiving
	{
		get => _isSignalNowEewReceiving;
		set => SetProperty(ref _isSignalNowEewReceiving, value);
	}

	private bool _dmdataReceiving;
	public bool DmdataReceiving
	{
		get => _dmdataReceiving;
		set => SetProperty(ref _dmdataReceiving, value);
	}

	private bool _dmdataWarningOnlyReceiving;
	public bool DmdataWarningOnlyReceiving
	{
		get => _dmdataWarningOnlyReceiving;
		set => SetProperty(ref _dmdataWarningOnlyReceiving, value);
	}

	private bool _dmdataDisconnected;
	public bool DmdataDisconnected
	{
		get => _dmdataDisconnected;
		set => SetProperty(ref _dmdataDisconnected, value);
	}

	private bool _axisReceiving;
	public bool AxisReceiving
	{
		get => _axisReceiving;
		set => SetProperty(ref _axisReceiving, value);
	}

	private bool _axisDisconnected;
	public bool AxisDisconnected
	{
		get => _axisDisconnected;
		set => SetProperty(ref _axisDisconnected, value);
	}

	private bool _allEewSourceFailed;
	public bool AllEewSourceFailed
	{
		get => _allEewSourceFailed;
		set => SetProperty(ref _allEewSourceFailed, value);
	}

	/// <summary>
	/// 警告メッセージ
	/// </summary>
	private string? _warningMessage;
	public string? WarningMessage
	{
		get => _warningMessage;
		set => SetProperty(ref _warningMessage, value);
	}

	private bool _showIntensityColorSample;
	public bool ShowIntensityColorSample
	{
		get => _showIntensityColorSample;
		set => SetProperty(ref _showIntensityColorSample, value);
	}

	private Eew[] _eews = [];
	public Eew[] Eews
	{
		get => _eews;
		set => SetProperty(ref _eews, value);
	}

	private KyoshinEvent[] _kyoshinEvents = [];
	public KyoshinEvent[] KyoshinEvents
	{
		get => _kyoshinEvents;
		set => SetProperty(ref _kyoshinEvents, value);
	}

	private ShakeDetectedRegion[] _shakeDetectedRegions = [];
	/// <summary>
	/// 揺れ検知地域
	/// </summary>
	public ShakeDetectedRegion[] ShakeDetectedRegions
	{
		get => _shakeDetectedRegions;
		set => SetProperty(ref _shakeDetectedRegions, value);
	}

	private KyoshinEventLevel _shakeDetectedLevel;
	/// <summary>
	/// 揺れ検知の最高レベル
	/// </summary>
	public KyoshinEventLevel ShakeDetectedLevel
	{
		get => _shakeDetectedLevel;
		set => SetProperty(ref _shakeDetectedLevel, value);
	}

	private bool _showShakeDetectedPanel;
	/// <summary>
	/// 揺れ検知パネルを表示するかどうか
	/// 通知レベル未満の場合は非表示
	/// </summary>
	public bool ShowShakeDetectedPanel
	{
		get => _showShakeDetectedPanel;
		set => SetProperty(ref _showShakeDetectedPanel, value);
	}

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
