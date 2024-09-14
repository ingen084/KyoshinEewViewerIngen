using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinMonitorLib;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

public abstract class EarthquakeInformationHost(bool isReplay, KyoshinEewViewerConfiguration config) : ReactiveObject
{
	public event Action<DateTime, IEew[]>? EewUpdated;
	protected void OnEewUpdated(DateTime time, IEew[] eews) => EewUpdated?.Invoke(time, eews);

	public event Action<(DateTime time, RealtimeObservationPoint[] data, KyoshinEvent[] events)>? RealtimeDataUpdated;
	protected void OnRealtimeDataUpdated((DateTime time, RealtimeObservationPoint[] data, KyoshinEvent[] events) data) => RealtimeDataUpdated?.Invoke(data);

	public event Action<(DateTime time, KyoshinEvent e, bool isLevelUp)>? KyoshinEventUpdated;
	protected void OnKyoshinEventUpdated((DateTime time, KyoshinEvent e, bool isLevelUp) data) => KyoshinEventUpdated?.Invoke(data);

	protected KyoshinEewViewerConfiguration Config { get; } = config;

	public abstract DateTime CurrentTime { get; }

	public bool IsReplay { get; } = isReplay;

	private MapNavigationRequest? _mapNavigationRequest;
	/// <summary>
	/// マップ表示位置のリクエスト
	/// </summary>
	public MapNavigationRequest? MapNavigationRequest
	{
		get => _mapNavigationRequest;
		protected set => this.RaiseAndSetIfChanged(ref _mapNavigationRequest, value);
	}

	private MapDisplayParameter _mapDisplayParameter;
	/// <summary>
	/// マップ表示用のパラメータ
	/// </summary>
	public MapDisplayParameter MapDisplayParameter
	{
		get => _mapDisplayParameter;
		protected set => this.RaiseAndSetIfChanged(ref _mapDisplayParameter, value);
	}

	private bool _isWorking;
	public bool IsWorking
	{
		get => _isWorking;
		set => this.RaiseAndSetIfChanged(ref _isWorking, value);
	}

	private DateTime _currentDisplayTime = DateTime.Now;
	public DateTime CurrentDisplayTime
	{
		get => _currentDisplayTime;
		set => this.RaiseAndSetIfChanged(ref _currentDisplayTime, value);
	}

	private bool _isSignalNowEewReceiving;
	public bool IsSignalNowEewReceiving
	{
		get => _isSignalNowEewReceiving;
		set => this.RaiseAndSetIfChanged(ref _isSignalNowEewReceiving, value);
	}

	private bool _dmdataReceiving;
	public bool DmdataReceiving
	{
		get => _dmdataReceiving;
		set => this.RaiseAndSetIfChanged(ref _dmdataReceiving, value);
	}

	private bool _dmdataWarningOnlyReceiving;
	public bool DmdataWarningOnlyReceiving
	{
		get => _dmdataWarningOnlyReceiving;
		set => this.RaiseAndSetIfChanged(ref _dmdataWarningOnlyReceiving, value);
	}

	private bool _dmdataDisconnected;
	public bool DmdataDisconnected
	{
		get => _dmdataDisconnected;
		set => this.RaiseAndSetIfChanged(ref _dmdataDisconnected, value);
	}

	/// <summary>
	/// 警告メッセージ
	/// </summary>
	private string? _warningMessage;
	public string? WarningMessage
	{
		get => _warningMessage;
		set => this.RaiseAndSetIfChanged(ref _warningMessage, value);
	}

	private bool _showIntensityColorSample;
	public bool ShowIntensityColorSample
	{
		get => _showIntensityColorSample;
		set => this.RaiseAndSetIfChanged(ref _showIntensityColorSample, value);
	}

	private IEew[] _eews = [];
	public IEew[] Eews
	{
		get => _eews;
		set => this.RaiseAndSetIfChanged(ref _eews, value);
	}

	private IEnumerable<RealtimeObservationPoint>? _realtimePoints = [];
	public IEnumerable<RealtimeObservationPoint>? RealtimePoints
	{
		get => _realtimePoints;
		set => this.RaiseAndSetIfChanged(ref _realtimePoints, value);
	}

	private KyoshinEvent[] _kyoshinEvents = [];
	public KyoshinEvent[] KyoshinEvents
	{
		get => _kyoshinEvents;
		set => this.RaiseAndSetIfChanged(ref _kyoshinEvents, value);
	}

	protected void UpateFocusPoint(DateTime time)
	{
		// 震度が不明でない、キャンセルされてない、最終報から1分未満、座標が設定されている場合のみズーム
		var targetEews = Eews.Where(e => /*(e.Source == EewSource.SignalNowProfessional && e.Intensity != JmaIntensity.Unknown) &&*/ !e.IsCancelled && (!e.IsFinal || (time - e.ReceiveTime).Minutes < 1) && e.Location != null);
		if (!targetEews.Any() && (!Config.KyoshinMonitor.UseExperimentalShakeDetect || !KyoshinEvents.Any(k => k.Level > KyoshinEventLevel.Weaker)))
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
		foreach (var l in targetEews.Select(e => e.Location))
		{
			CheckLocation2(l!);
			CheckLocation(new(l!.Latitude - 1, l.Longitude - 1));
			CheckLocation(new(l.Latitude + 1, l.Longitude + 1));
		}
		// Event
		foreach (var e in KyoshinEvents.Where(k => k.Level > KyoshinEventLevel.Weaker))
		{
			CheckLocation2(e.TopLeft);
			CheckLocation2(e.BottomRight);
			CheckLocation(new(e.TopLeft.Latitude - .5f, e.TopLeft.Longitude - .5f));
			CheckLocation(new(e.BottomRight.Latitude + .5f, e.BottomRight.Longitude + .5f));
		}

		// EEW によるズームが行われるときのみ左側の領域確保を行う
		// MapPadding = targetEews.Any() ? new Thickness(310, 0, 0, 0) : new Thickness(0);

		// 初回移動時は MustBound を設定しないようにしてズームを適切に動作させるようにする
		MapNavigationRequest = new(new(minLat, minLng, maxLat - minLat, maxLng - minLng), MapNavigationRequest != null ? new(minLat2, minLng2, maxLat2 - minLat2, maxLng2 - minLng2) : null);
	}
}
