using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinMonitorLib;
using R3;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KyoshinEewViewer.Series.Earthquake.Models;

public class EarthquakeEvent : ObservableObject
{
	public EarthquakeEvent(string eventId)
	{
		EventId = eventId;

		Observable.CombineLatest(
			this.ObservePropertyChanged(x => x.IsHypocenterOnly),
			this.ObservePropertyChanged(x => x.IsDetailIntensityApplied),
			(only, applied) => only || applied
		).Subscribe(x => IsHypocenterAvailable = x);

		Observable.CombineLatest(
			this.ObservePropertyChanged(x => x.IsHypocenterOnly),
			this.ObservePropertyChanged(x => x.IsSokuhou),
			this.ObservePropertyChanged(x => x.IsVolcano),
			this.ObservePropertyChanged(x => x.IsForeign),
			(only, sokuhou, volcano, foreign) =>
			{
				if (sokuhou && only)
					return "震度速報+震源情報";
				if (sokuhou)
					return "震度速報";
				if (only)
					return "震源情報";
				if (volcano)
					return "大規模噴火";
				if (foreign)
					return "遠地地震情報";
				return "震源･震度情報";
			}
		).Subscribe(x => Title = x);

		this.ObservePropertyChanged(x => x.Depth)
			.Subscribe(depth => IsVeryShallow = depth <= 0);

		this.ObservePropertyChanged(x => x.Depth)
			.Subscribe(depth => IsNoDepthData = depth <= -1);

		this.ObservePropertyChanged(x => x.Intensity)
			.Subscribe(intensity => IsUnknownIntensity = intensity == JmaIntensity.Unknown);
	}

	private bool _isSelecting;
	/// <summary>
	/// 該当項目が選択中か
	/// </summary>
	public bool IsSelecting
	{
		get => _isSelecting;
		set => SetProperty(ref _isSelecting, value);
	}

	private List<string> ProcessedTelegramIds { get; } = [];
	public ObservableCollection<EarthquakeInformationFragment> Fragments { get; } = [];

	// メモ イベントIDの振り分けは上位でやる
	public EarthquakeInformationFragment? ProcessTelegram(Telegram telegram, JmaXmlDocument document)
	{
		if (ProcessedTelegramIds.Contains(telegram.Key))
			return null;
		ProcessedTelegramIds.Add(telegram.Key);

		// 取り消し処理
		if (document.Head.InfoType == "取消")
		{
			foreach (var f in Fragments)
			{
				// 同種の電文をすべて取り消し扱いに
				if (f.Title == document.Control.Title)
					f.IsCancelled = true;
			}
			SyncProperties();
			return null;
		}
		// 訂正の場合、一番最後の情報を訂正済みにして、そのほかは普通に処理する
		if (document.Head.InfoType == "訂正" && Fragments.LastOrDefault(x => x.Title == document.Control.Title) is { } lastFragment)
			lastFragment.IsCorrected = true;

		// 電文をパース
		var fragment = EarthquakeInformationFragment.CreateFromJmxXmlDocument(telegram, document);
		Fragments.Add(fragment);

		SyncProperties();

		return fragment;
	}

	public void AddFragment(EarthquakeInformationFragment fragment)
	{
		Fragments.Add(fragment);
		SyncProperties();
	}

	/// <summary>
	/// 震源・震度情報の同期
	/// </summary>
	private void SyncProperties()
	{
		// 取り消し状態を同期
		IsCancelled = Fragments.All(x => x.IsCancelled);

		// 訓練･試験チェック 1回でも読んだ記録があれば訓練扱いとする
		IsTraining = Fragments.Where(x => !x.IsCancelled && !x.IsCorrected).Any(x => x.IsTraining);
		IsTest = Fragments.Where(x => !x.IsCancelled && !x.IsCorrected).Any(x => x.IsTest);

		foreach (var fragment in Fragments)
		{
			// 有効でないものはスルー
			if (fragment.IsCancelled || fragment.IsCorrected)
				continue;

			UpdatedTime = fragment.ArrivedTime;

			// 震度速報
			if (fragment is IntensityInformationFragment i)
			{
				Intensity = i.MaxIntensity;
				// 震源情報･震源震度情報がない場合のみ震源情報を更新
				if (!IsDetailIntensityApplied)
				{
					IsSokuhou = true;
					if (!IsHypocenterOnly)
					{
						Time = i.DetectionTime;
						IsDetectionTime = true;
						Place = i.Place;
						IsOnlypoint = i.IsOnlypoint;
						Depth = -1;
					}
				}
				Comment = i.Comment;
				FreeFormComment = i.FreeFormComment;
			}

			// 震源情報の更新
			if (fragment is HypocenterInformationFragment h)
			{
				Time = h.OccurrenceTime;
				IsDetectionTime = false;
				Place = h.Place;
				Location = h.Location;

				LocationError = h.LocationError;
				IsOnlypoint = true;
				Magnitude = h.Magnitude;
				MagnitudeAlternativeText = h.MagnitudeAlternativeText;
				Depth = h.Depth;
				DepthError = h.DepthError;
				// 震源震度情報を受信していた場合は震源のみのフラグを立てない
				IsHypocenterOnly = !IsDetailIntensityApplied;

				// コメント部分
				Comment = h.Comment ?? Comment;
				FreeFormComment = h.FreeFormComment;
			}

			// 震源震度情報
			if (fragment is HypocenterAndIntensityInformationFragment hi)
			{
				IsSokuhou = false;
				IsHypocenterOnly = false;

				IsForeign = hi.IsForeign;
				IsVolcano = hi.IsVolcano;
				VolcanoName = hi.VolcanoName;
				Intensity = hi.MaxIntensity;

				IsDetailIntensityApplied = true;
			}

			// 長周期地震動に関する観測情報
			if (fragment is LpgmIntensityInformationFragment lpgm)
			{
				LpgmIntensity = lpgm.MaxLpgmIntensity;
			}
		}
	}

	/// <summary>
	/// 地震の EventId
	/// </summary>
	public string EventId { get; }

	private string? _title;
	/// <summary>
	/// イベントのタイトル(現在の情報種別)
	/// </summary>
	public string? Title
	{
		get => _title;
		private set => SetProperty(ref _title, value);
	}

	private string? _subtitle;
	/// <summary>
	/// 補足情報(存在する場合は外部から設定する)
	/// </summary>
	public string? Subtitle
	{
		get => _subtitle;
		set => SetProperty(ref _subtitle, value);
	}

	private DateTime _updatedTime;
	/// <summary>
	/// 最新の電文の発表時刻
	/// </summary>
	public DateTime UpdatedTime
	{
		get => _updatedTime;
		set => SetProperty(ref _updatedTime, value);
	}

	private bool _isSokuhou;
	/// <summary>
	/// 震度速報
	/// </summary>
	public bool IsSokuhou
	{
		get => _isSokuhou;
		set => SetProperty(ref _isSokuhou, value);
	}

	private bool _isForeign;
	/// <summary>
	/// 遠地地震
	/// </summary>
	public bool IsForeign
	{
		get => _isForeign;
		set => SetProperty(ref _isForeign, value);
	}

	private bool _isVolcano;
	/// <summary>
	/// 火山噴火
	/// </summary>
	public bool IsVolcano
	{
		get => _isVolcano;
		set => SetProperty(ref _isVolcano, value);
	}

	private string? _volcanoName;
	/// <summary>
	/// 火山名
	/// </summary>
	public string? VolcanoName
	{
		get => _volcanoName;
		set => SetProperty(ref _volcanoName, value);
	}

	private bool _isOnlypoint;
	/// <summary>
	/// 震度速報かつ最大震度の観測が1地域のみ
	/// </summary>
	public bool IsOnlypoint
	{
		get => _isOnlypoint;
		set => SetProperty(ref _isOnlypoint, value);
	}

	private bool _isTraining;
	/// <summary>
	/// 訓練
	/// </summary>
	public bool IsTraining
	{
		get => _isTraining;
		set => SetProperty(ref _isTraining, value);
	}

	private bool _isTest;
	/// <summary>
	/// 試験
	/// </summary>
	public bool IsTest
	{
		get => _isTest;
		set => SetProperty(ref _isTest, value);
	}

	private bool _isHypocenterOnly;
	/// <summary>
	/// 震源のみ
	/// </summary>
	public bool IsHypocenterOnly
	{
		get => _isHypocenterOnly;
		set => SetProperty(ref _isHypocenterOnly, value);
	}

	private bool _isDetailIntensityApplied;
	/// <summary>
	/// 震源震度情報を適用済み<br/>これ以降は震度速報は震度情報のみ更新する
	/// </summary>
	public bool IsDetailIntensityApplied
	{
		get => _isDetailIntensityApplied;
		set => SetProperty(ref _isDetailIntensityApplied, value);
	}

	private bool _isCancelled;
	/// <summary>
	/// 属しているすべての電文(=該当イベントID)がキャンセル扱いになっている
	/// </summary>
	public bool IsCancelled
	{
		get => _isCancelled;
		set => SetProperty(ref _isCancelled, value);
	}

	private DateTime _time;
	/// <summary>
	/// 発生もしくは検知時刻
	/// </summary>
	public DateTime Time
	{
		get => _time;
		set => SetProperty(ref _time, value);
	}

	private bool _isDetectTime;
	/// <summary>
	/// 時刻は検知時刻を示しているか
	/// </summary>
	public bool IsDetectionTime
	{
		get => _isDetectTime;
		set => SetProperty(ref _isDetectTime, value);
	}

	private string? _place;
	/// <summary>
	/// 震央地名もしくは観測地名(震度速報)
	/// </summary>
	public string? Place
	{
		get => _place;
		set => SetProperty(ref _place, value);
	}

	private Location? _location;
	/// <summary>
	/// 震央座標
	/// </summary>
	public Location? Location
	{
		get => _location;
		set => SetProperty(ref _location, value);
	}

	private Location? _locationError;
	/// <summary>
	/// 震央座標の誤差 (±度)
	/// </summary>
	public Location? LocationError
	{
		get => _locationError;
		set => SetProperty(ref _locationError, value);
	}

	private JmaIntensity _intensity = JmaIntensity.Unknown;
	/// <summary>
	/// 最大震度
	/// </summary>
	public JmaIntensity Intensity
	{
		get => _intensity;
		set => SetProperty(ref _intensity, value);
	}

	private LpgmIntensity? _lpgmIntensity;
	/// <summary>
	/// 最大の長周期地震動階級
	/// </summary>
	public LpgmIntensity? LpgmIntensity
	{
		get => _lpgmIntensity;
		set => SetProperty(ref _lpgmIntensity, value);
	}

	private float _magnitude;
	/// <summary>
	/// 規模
	/// </summary>
	public float Magnitude
	{
		get => _magnitude;
		set => SetProperty(ref _magnitude, value);
	}

	private string? _magnitudeAlternativeText;
	/// <summary>
	/// 規模の代替テキスト
	/// </summary>
	public string? MagnitudeAlternativeText
	{
		get => _magnitudeAlternativeText;
		set => SetProperty(ref _magnitudeAlternativeText, value);
	}

	private int _depth = -1;
	/// <summary>
	/// 深さ(km)
	/// </summary>
	public int Depth
	{
		get => _depth;
		set => SetProperty(ref _depth, value);
	}

	private int? _depthError;
	/// <summary>
	/// 深さの誤差 (±km)
	/// </summary>
	public int? DepthError
	{
		get => _depthError;
		set => SetProperty(ref _depthError, value);
	}

	private string? _comment;
	/// <summary>
	/// コメント
	/// </summary>
	public string? Comment
	{
		get => _comment;
		set => SetProperty(ref _comment, value);
	}

	private string? _freeFormComment;
	/// <summary>
	/// 自由形式のコメント
	/// </summary>
	public string? FreeFormComment
	{
		get => _freeFormComment;
		set => SetProperty(ref _freeFormComment, value);
	}

	private bool _isHypocenterAvailable;
	public bool IsHypocenterAvailable
	{
		get => _isHypocenterAvailable;
		private set => SetProperty(ref _isHypocenterAvailable, value);
	}

	private bool _isVeryShallow;
	public bool IsVeryShallow
	{
		get => _isVeryShallow;
		private set => SetProperty(ref _isVeryShallow, value);
	}

	private bool _isNoDepthData;
	public bool IsNoDepthData
	{
		get => _isNoDepthData;
		private set => SetProperty(ref _isNoDepthData, value);
	}

	private bool _isUnknownIntensity;
	public bool IsUnknownIntensity
	{
		get => _isUnknownIntensity;
		private set => SetProperty(ref _isUnknownIntensity, value);
	}

	[Obsolete("GetNotificationMessage()は非推奨です。代わりにScribanテンプレートを使用してください。")]
	public string GetNotificationMessage()
	{
		var parts = new List<string>();
		if (IsCancelled)
			parts.Add("[取消]");
		if (IsTraining)
			parts.Add("[訓練]");
		if (IsTest)
			parts.Add("[試験]");
		if (Intensity != JmaIntensity.Unknown)
			parts.Add($"最大{Intensity.ToLongString()}");

		if (IsHypocenterOnly || IsDetailIntensityApplied)
		{
			parts.Insert(0, $"{Time:HH:mm}");
			parts.Add(Place ?? "不明");
			if (!IsNoDepthData)
			{
				if (IsVeryShallow)
					parts.Add("ごく浅い");
				else
					parts.Add(Depth + "km");
			}
			parts.Add(MagnitudeAlternativeText ?? $"M{Magnitude:0.0}");
		}
		return string.Join('/', parts);
	}
}
