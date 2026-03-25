using KyoshinEewViewer.Core;
using KyoshinMonitorLib;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KyoshinEewViewer.Series.Earthquake.Models;

public class EarthquakeEvent : ReactiveObject
{
	public EarthquakeEvent(string eventId)
	{
		EventId = eventId;

		_isHypocenterAvailable = this.WhenAny(
			x => x.IsHypocenterOnly,
			x => x.IsDetailIntensityApplied,
			(only, applied) => only.Value || applied.Value
		).ToProperty(this, x => x.IsHypocenterAvailable);

		_title = this.WhenAny(
			x => x.IsHypocenterOnly,
			x => x.IsSokuhou,
			x => x.IsVolcano,
			x => x.IsForeign,
			x => x.IsUnreliableEventIdSource,
			(only, sokuhou, volcano, foreign, unreliable) =>
			{
				var title = (sokuhou.Value, only.Value, volcano.Value, foreign.Value) switch
				{
					(true, true, _, _) => "震度速報+震源情報",
					(true, _, _, _) => "震度速報",
					(_, true, _, _) => "震源情報",
					(_, _, true, _) => "大規模噴火",
					(_, _, _, true) => "遠地地震情報",
					_ => "震源･震度情報",
				};
				return unreliable.Value ? $"{title}(参考)" : title;
			}
		).ToProperty(this, x => x.Title);

		_isVeryShallow = this.WhenAny(
			x => x.Depth,
			depth => Depth <= 0
		).ToProperty(this, x => x.IsVeryShallow);

		_isNoDepthData = this.WhenAny(
			x => x.Depth,
			depth => depth.Value <= -1
		).ToProperty(this, x => x.IsNoDepthData);

		_isUnknownIntensity = this.WhenAny(
			x => x.Intensity,
			intensity => intensity.Value == JmaIntensity.Unknown
		).ToProperty(this, x => x.IsUnknownIntensity);
	}

	private bool _isSelecting;
	/// <summary>
	/// 該当項目が選択中か
	/// </summary>
	public bool IsSelecting
	{
		get => _isSelecting;
		set => this.RaiseAndSetIfChanged(ref _isSelecting, value);
	}

	private List<string> ProcessedTelegramIds { get; } = [];
	public ObservableCollection<EarthquakeInformationFragment> Fragments { get; } = [];

	/// <summary>
	/// 中間表現データからFragmentを生成して追加する
	/// </summary>
	/// <param name="data">中間表現データ</param>
	/// <param name="displayDataProvider">遅延パース用の表示データプロバイダ</param>
	/// <param name="telegramKey">電文の重複判定キー（JMA XML用。nullの場合は自動生成）</param>
	/// <returns>生成されたFragment。重複の場合はnull</returns>
	public EarthquakeInformationFragment? ProcessIntermediateData(
		EarthquakeInformationData data,
		IEarthquakeDisplayDataProvider? displayDataProvider = null,
		string? telegramKey = null)
	{
		var key = telegramKey ?? data.TelegramKey ?? $"{data.Source}:{data.EventId}:{data.ReportDateTime:yyyyMMddHHmmss}";
		if (ProcessedTelegramIds.Contains(key))
			return null;
		ProcessedTelegramIds.Add(key);

		// 異なるソースから同一内容の電文を受信した場合は通知を抑制する
		if (data.InfoType == EarthquakeInfoType.Normal &&
			Fragments.Any(f => !f.IsCancelled && !f.IsCorrected && f.Title == data.Title && f.ArrivedTime == data.ReportDateTime))
			return null;

		// 取り消し処理
		if (data.InfoType == EarthquakeInfoType.Cancel)
		{
			foreach (var f in Fragments)
			{
				if (f.Title == data.Title)
					f.IsCancelled = true;
			}
			SyncProperties();
			return null;
		}

		// 訂正の場合、一番最後の情報を訂正済みにする
		if (data.InfoType == EarthquakeInfoType.Correction && Fragments.LastOrDefault(x => x.Title == data.Title) is { } lastFragment)
			lastFragment.IsCorrected = true;

		var fragment = EarthquakeInformationFragment.CreateFromIntermediateData(data, displayDataProvider);
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
				IsOnlypoint = true;
				Magnitude = h.Magnitude;
				MagnitudeAlternativeText = h.MagnitudeAlternativeText;
				Depth = h.Depth;
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

	private readonly ObservableAsPropertyHelper<string?> _title;
	/// <summary>
	/// イベントのタイトル(現在の情報種別)
	/// </summary>
	public string? Title => _title?.Value;

	private string? _subtitle;
	/// <summary>
	/// 補足情報(存在する場合は外部から設定する)
	/// </summary>
	public string? Subtitle
	{
		get => _subtitle;
		set => this.RaiseAndSetIfChanged(ref _subtitle, value);
	}

	private DateTime _updatedTime;
	/// <summary>
	/// 最新の電文の発表時刻
	/// </summary>
	public DateTime UpdatedTime
	{
		get => _updatedTime;
		set => this.RaiseAndSetIfChanged(ref _updatedTime, value);
	}

	private bool _isSokuhou;
	/// <summary>
	/// 震度速報
	/// </summary>
	public bool IsSokuhou
	{
		get => _isSokuhou;
		set => this.RaiseAndSetIfChanged(ref _isSokuhou, value);
	}

	private bool _isForeign;
	/// <summary>
	/// 遠地地震
	/// </summary>
	public bool IsForeign
	{
		get => _isForeign;
		set => this.RaiseAndSetIfChanged(ref _isForeign, value);
	}

	private bool _isVolcano;
	/// <summary>
	/// 火山噴火
	/// </summary>
	public bool IsVolcano
	{
		get => _isVolcano;
		set => this.RaiseAndSetIfChanged(ref _isVolcano, value);
	}

	private string? _volcanoName;
	/// <summary>
	/// 火山名
	/// </summary>
	public string? VolcanoName
	{
		get => _volcanoName;
		set => this.RaiseAndSetIfChanged(ref _volcanoName, value);
	}

	private bool _isOnlypoint;
	/// <summary>
	/// 震度速報かつ最大震度の観測が1地域のみ
	/// </summary>
	public bool IsOnlypoint
	{
		get => _isOnlypoint;
		set => this.RaiseAndSetIfChanged(ref _isOnlypoint, value);
	}

	private bool _isTraining;
	/// <summary>
	/// 訓練
	/// </summary>
	public bool IsTraining
	{
		get => _isTraining;
		set => this.RaiseAndSetIfChanged(ref _isTraining, value);
	}

	private bool _isTest;
	/// <summary>
	/// 試験
	/// </summary>
	public bool IsTest
	{
		get => _isTest;
		set => this.RaiseAndSetIfChanged(ref _isTest, value);
	}

	private bool _isUnreliableEventIdSource;
	/// <summary>
	/// EventIdが信頼できないソース由来か（P2P地震情報など）
	/// </summary>
	public bool IsUnreliableEventIdSource
	{
		get => _isUnreliableEventIdSource;
		set => this.RaiseAndSetIfChanged(ref _isUnreliableEventIdSource, value);
	}

	private bool _isHypocenterOnly;
	/// <summary>
	/// 震源のみ
	/// </summary>
	public bool IsHypocenterOnly
	{
		get => _isHypocenterOnly;
		set => this.RaiseAndSetIfChanged(ref _isHypocenterOnly, value);
	}

	private bool _isDetailIntensityApplied;
	/// <summary>
	/// 震源震度情報を適用済み<br/>これ以降は震度速報は震度情報のみ更新する
	/// </summary>
	public bool IsDetailIntensityApplied
	{
		get => _isDetailIntensityApplied;
		set => this.RaiseAndSetIfChanged(ref _isDetailIntensityApplied, value);
	}

	private bool _isCancelled;
	/// <summary>
	/// 属しているすべての電文(=該当イベントID)がキャンセル扱いになっている
	/// </summary>
	public bool IsCancelled
	{
		get => _isCancelled;
		set => this.RaiseAndSetIfChanged(ref _isCancelled, value);
	}

	private DateTime _time;
	/// <summary>
	/// 発生もしくは検知時刻
	/// </summary>
	public DateTime Time
	{
		get => _time;
		set => this.RaiseAndSetIfChanged(ref _time, value);
	}

	private bool _isDetectTime;
	/// <summary>
	/// 時刻は検知時刻を示しているか
	/// </summary>
	public bool IsDetectionTime
	{
		get => _isDetectTime;
		set => this.RaiseAndSetIfChanged(ref _isDetectTime, value);
	}

	private string? _place;
	/// <summary>
	/// 震央地名もしくは観測地名(震度速報)
	/// </summary>
	public string? Place
	{
		get => _place;
		set => this.RaiseAndSetIfChanged(ref _place, value);
	}

	private Location? _location;
	/// <summary>
	/// 震央座標
	/// </summary>
	public Location? Location
	{
		get => _location;
		set => this.RaiseAndSetIfChanged(ref _location, value);
	}

	private JmaIntensity _intensity = JmaIntensity.Unknown;
	/// <summary>
	/// 最大震度
	/// </summary>
	public JmaIntensity Intensity
	{
		get => _intensity;
		set => this.RaiseAndSetIfChanged(ref _intensity, value);
	}

	private LpgmIntensity? _lpgmIntensity;
	/// <summary>
	/// 最大の長周期地震動階級
	/// </summary>
	public LpgmIntensity? LpgmIntensity
	{
		get => _lpgmIntensity;
		set => this.RaiseAndSetIfChanged(ref _lpgmIntensity, value);
	}

	private float _magnitude;
	/// <summary>
	/// 規模
	/// </summary>
	public float Magnitude
	{
		get => _magnitude;
		set => this.RaiseAndSetIfChanged(ref _magnitude, value);
	}

	private string? _magnitudeAlternativeText;
	/// <summary>
	/// 規模の代替テキスト
	/// </summary>
	public string? MagnitudeAlternativeText
	{
		get => _magnitudeAlternativeText;
		set => this.RaiseAndSetIfChanged(ref _magnitudeAlternativeText, value);
	}

	private int _depth = -1;
	/// <summary>
	/// 深さ(km)
	/// </summary>
	public int Depth
	{
		get => _depth;
		set => this.RaiseAndSetIfChanged(ref _depth, value);
	}

	private string? _comment;
	/// <summary>
	/// コメント
	/// </summary>
	public string? Comment
	{
		get => _comment;
		set => this.RaiseAndSetIfChanged(ref _comment, value);
	}

	private string? _freeFormComment;
	/// <summary>
	/// 自由形式のコメント
	/// </summary>
	public string? FreeFormComment
	{
		get => _freeFormComment;
		set => this.RaiseAndSetIfChanged(ref _freeFormComment, value);
	}

	private readonly ObservableAsPropertyHelper<bool> _isHypocenterAvailable;
	public bool IsHypocenterAvailable => _isHypocenterAvailable.Value;

	private readonly ObservableAsPropertyHelper<bool> _isVeryShallow;
	public bool IsVeryShallow => _isVeryShallow.Value;

	private readonly ObservableAsPropertyHelper<bool> _isNoDepthData;
	public bool IsNoDepthData => _isNoDepthData.Value;

	private readonly ObservableAsPropertyHelper<bool> _isUnknownIntensity;
	public bool IsUnknownIntensity => _isUnknownIntensity.Value;

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
