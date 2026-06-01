using DmdataSharp.ApiResponses.V2.Parameters;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinMonitorLib;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace KyoshinEewViewer.Series.Earthquake.Models;

public class EarthquakeEvent : ReactiveObject
{
	private const string NoticeableEarthquakeUpdateTitle = "顕著な地震の震源要素更新のお知らせ";

	public EarthquakeEvent(string eventId)
	{
		EventId = eventId;
	}

	public string EventId { get; }

	public ObservableCollection<EarthquakeInformationFragment> Fragments { get; } = [];

	private List<string> ProcessedTelegramIds { get; } = [];

	private bool _isSelecting;
	/// <summary>
	/// 該当項目が選択中か
	/// </summary>
	public bool IsSelecting
	{
		get => _isSelecting;
		set => this.RaiseAndSetIfChanged(ref _isSelecting, value);
	}

	private string? _subtitle;
	/// <summary>
	/// 補足情報(存在する場合は外部から設定する)
	/// </summary>
	public string? Subtitle
	{
		get => _subtitle;
		set => this.RaiseAndSetIfChanged(ref _subtitle, value);
	}

	private EarthquakeSnapshot? _currentSnapshot;
	/// <summary>
	/// 現在の有効な集約状態スナップショット
	/// </summary>
	public EarthquakeSnapshot? CurrentSnapshot
	{
		get => _currentSnapshot;
		private set
		{
			_currentSnapshot = value;
			this.RaisePropertyChanged(nameof(CurrentSnapshot));
			this.RaisePropertyChanged(string.Empty);
		}
	}

	private bool _hasAxisFragment;

	public EarthquakeInformationFragment? ProcessTelegram(
		Telegram telegram,
		JmaXmlDocument document,
		IReadOnlyList<EarthquakeStationParameterResponse.Item>? stations = null)
	{
		if (ProcessedTelegramIds.Contains(telegram.Key))
			return null;
		ProcessedTelegramIds.Add(telegram.Key);

		// 取消
		if (document.Head.InfoType == "取消")
			return ApplyCancellation(document.Control.Title);

		// 訂正
		if (document.Head.InfoType == "訂正")
			ApplyCorrectionFlag(document.Control.Title);

		var fragment = EarthquakeInformationFragment.CreateFromJmxXmlDocument(telegram, document, stations);

		// AXIS Fragment が混入しているイベントでは XML 側でも等価判定を通す
		if (_hasAxisFragment && Fragments.Any(f => f.IsEquivalentContent(fragment)))
		{
			LogHost.Default.Debug($"AXIS由来Fragmentと等価なXML電文を検出しスキップしました: Title={document.Control.Title} EventID={EventId} Serial={document.Head.Serial}");
			return null;
		}

		Fragments.Add(fragment);
		SyncProperties();
		return fragment;
	}

	public EarthquakeInformationFragment? ProcessAxisMessage(
		EarthquakeMessage message,
		IReadOnlyList<EarthquakeStationParameterResponse.Item>? stations = null)
	{
		// 取消
		if (message.Head.InfoType == "取消")
			return ApplyCancellation(message.Control.Title);

		// 訂正
		if (message.Head.InfoType == "訂正")
			ApplyCorrectionFlag(message.Control.Title);

		var fragment = EarthquakeInformationFragment.CreateFromAxisJson(message, stations);
		if (fragment == null)
			return null;

		// AXIS 経路は常時等価判定
		if (Fragments.Any(f => f.IsEquivalentContent(fragment)))
		{
			LogHost.Default.Debug($"AXIS地震情報の重複を検出しスキップしました: Title={message.Control.Title} EventID={EventId} Serial={message.Head.Serial}");
			return null;
		}

		Fragments.Add(fragment);
		_hasAxisFragment = true;
		SyncProperties();
		return fragment;
	}

	private EarthquakeInformationFragment? ApplyCancellation(string title)
	{
		EarthquakeInformationFragment? changed = null;
		foreach (var f in Fragments)
		{
			if (f.Title == title && !f.IsCancelled)
			{
				f.IsCancelled = true;
				changed = f;
			}
		}
		if (changed != null)
			SyncProperties();
		return changed;
	}

	private void ApplyCorrectionFlag(string title)
	{
		if (Fragments.LastOrDefault(x => x.Title == title) is { IsCorrected: false } last)
			last.IsCorrected = true;
	}

	public void AddFragment(EarthquakeInformationFragment fragment)
	{
		Fragments.Add(fragment);
		SyncProperties();
	}

	internal void Resync() => SyncProperties();

	internal void SetSnapshot(EarthquakeSnapshot snapshot) => CurrentSnapshot = snapshot;

	private void SyncProperties() => CurrentSnapshot = ComposeCurrentSnapshot();

	private EarthquakeSnapshot? ComposeCurrentSnapshot()
	{
		var validFragments = Fragments.Where(x => !x.IsCancelled && !x.IsCorrected).ToList();
		if (validFragments.Count == 0)
			return null;

		var hypoFragment = PickHypocenterFragment(validFragments);
		var sokuhouFragment = PickSokuhouFragment(validFragments);
		var lpgmFragment = PickLpgmFragment(validFragments);
		var detailFragment = validFragments
			.OfType<HypocenterAndIntensityInformationFragment>()
			.Where(f => f is not LpgmIntensityInformationFragment)
			.LastOrDefault();

		EarthquakeHypocenterSnapshot? hypocenter = null;
		if (hypoFragment != null)
		{
			var (isForeign, isVolcano, volcanoName) = hypoFragment switch
			{
				HypocenterAndIntensityInformationFragment hi => (hi.IsForeign, hi.IsVolcano, hi.VolcanoName),
				_ => (false, false, (string?)null),
			};

			hypocenter = new EarthquakeHypocenterSnapshot(
				OriginTime: hypoFragment.OccurrenceTime,
				Place: hypoFragment.Place,
				Location: hypoFragment.Location,
				LocationError: hypoFragment.LocationError,
				Magnitude: hypoFragment.Magnitude,
				MagnitudeAlternativeText: hypoFragment.MagnitudeAlternativeText,
				Depth: hypoFragment.Depth,
				DepthError: hypoFragment.DepthError,
				IsForeign: isForeign,
				IsVolcano: isVolcano,
				VolcanoName: volcanoName);
		}

		EarthquakeIntensityScope scope;
		IReadOnlyList<PrefectureIntensitySnapshot> prefectures;
		JmaIntensity maxIntensity;

		if (detailFragment != null)
		{
			scope = EarthquakeIntensityScope.Detail;
			prefectures = detailFragment.Observation;
			maxIntensity = detailFragment.MaxIntensity;
		}
		else if (lpgmFragment != null)
		{
			scope = EarthquakeIntensityScope.Lpgm;
			prefectures = lpgmFragment.Observation;
			maxIntensity = lpgmFragment.MaxIntensity;
		}
		else if (sokuhouFragment != null)
		{
			scope = EarthquakeIntensityScope.AreaOnly;
			prefectures = sokuhouFragment.Observation;
			maxIntensity = sokuhouFragment.MaxIntensity;
		}
		else
		{
			scope = EarthquakeIntensityScope.HypocenterOnly;
			prefectures = [];
			maxIntensity = JmaIntensity.Unknown;
		}

		var hypocenterInfoOnly = validFragments
			.OfType<HypocenterInformationFragment>()
			.Where(f => f is not HypocenterAndIntensityInformationFragment
					 && f.Title != NoticeableEarthquakeUpdateTitle)
			.OrderByDescending(f => f.ArrivedTime)
			.FirstOrDefault();

		EarthquakeInformationFragment? commentSource =
			(EarthquakeInformationFragment?)lpgmFragment
			?? detailFragment
			?? (EarthquakeInformationFragment?)hypocenterInfoOnly
			?? sokuhouFragment;

		string? comment = null;
		string? freeFormComment = null;
		switch (commentSource)
		{
			case HypocenterAndIntensityInformationFragment hi:
				comment = hi.Comment;
				freeFormComment = hi.FreeFormComment;
				break;
			case HypocenterInformationFragment h:
				comment = h.Comment;
				freeFormComment = h.FreeFormComment;
				break;
			case IntensityInformationFragment i:
				comment = i.Comment;
				freeFormComment = i.FreeFormComment;
				break;
		}

		return new EarthquakeSnapshot(
			Scope: scope,
			Hypocenter: hypocenter,
			MaxIntensity: maxIntensity,
			MaxLpgmIntensity: lpgmFragment?.MaxLpgmIntensity,
			DetectionTime: sokuhouFragment?.DetectionTime,
			SokuhouPlace: sokuhouFragment?.Place,
			IsSingleArea: hypocenter != null ? true : (sokuhouFragment?.IsSingleArea ?? true),
			Comment: comment,
			FreeFormComment: freeFormComment,
			UpdatedTime: validFragments.Max(f => f.ArrivedTime),
			Prefectures: prefectures);
	}

	private static HypocenterInformationFragment? PickHypocenterFragment(
		IReadOnlyList<EarthquakeInformationFragment> validFragments)
	{
		var hypoFragments = validFragments.OfType<HypocenterInformationFragment>().ToList();
		if (hypoFragments.Count == 0) return null;

		var noticeable = hypoFragments.LastOrDefault(f => f.Title == NoticeableEarthquakeUpdateTitle);
		if (noticeable != null) return noticeable;

		return hypoFragments
			.OrderBy(f => f is HypocenterAndIntensityInformationFragment ? 0 : 1)
			.ThenByDescending(f => f.ArrivedTime)
			.FirstOrDefault();
	}

	private static IntensityInformationFragment? PickSokuhouFragment(
		IReadOnlyList<EarthquakeInformationFragment> validFragments)
		=> validFragments
			.OfType<IntensityInformationFragment>()
			.OrderByDescending(f => f.ArrivedTime)
			.FirstOrDefault();

	private static LpgmIntensityInformationFragment? PickLpgmFragment(
		IReadOnlyList<EarthquakeInformationFragment> validFragments)
		=> validFragments
			.OfType<LpgmIntensityInformationFragment>()
			.OrderByDescending(f => f.ArrivedTime)
			.FirstOrDefault();

	public string? Place => CurrentSnapshot?.Hypocenter?.Place ?? CurrentSnapshot?.SokuhouPlace;
	public Location? Location => CurrentSnapshot?.Hypocenter?.Location;
	public Location? LocationError => CurrentSnapshot?.Hypocenter?.LocationError;
	public float Magnitude => CurrentSnapshot?.Hypocenter?.Magnitude ?? float.NaN;
	public string? MagnitudeAlternativeText => CurrentSnapshot?.Hypocenter?.MagnitudeAlternativeText;
	public int Depth => CurrentSnapshot?.Hypocenter?.Depth ?? -1;
	public int? DepthError => CurrentSnapshot?.Hypocenter?.DepthError;
	public DateTime? OriginTime => CurrentSnapshot?.Hypocenter?.OriginTime;
	public DateTime? ArrivalTime => CurrentSnapshot?.DetectionTime;
	public DateTime Time => OriginTime ?? ArrivalTime ?? default;
	public JmaIntensity Intensity => CurrentSnapshot?.MaxIntensity ?? JmaIntensity.Unknown;
	public LpgmIntensity? LpgmIntensity => CurrentSnapshot?.MaxLpgmIntensity;
	public string? Comment => CurrentSnapshot?.Comment;
	public string? FreeFormComment => CurrentSnapshot?.FreeFormComment;
	public bool IsSingleArea => CurrentSnapshot?.IsSingleArea ?? false;
	public DateTime UpdatedTime => CurrentSnapshot?.UpdatedTime ?? default;

	public EarthquakeIntensityScope? Scope => CurrentSnapshot?.Scope;
	public bool IsHypocenterAvailable => CurrentSnapshot?.Hypocenter != null;
	public bool IsSokuhou => Scope == EarthquakeIntensityScope.AreaOnly;
	public bool IsDetailIntensityApplied => Scope is EarthquakeIntensityScope.Detail or EarthquakeIntensityScope.Lpgm;
	public bool IsHypocenterOnly => IsHypocenterAvailable && !IsDetailIntensityApplied;
	public bool IsDetectionTime => OriginTime == null;

	public bool IsForeign => CurrentSnapshot?.Hypocenter?.IsForeign ?? false;
	public bool IsVolcano => CurrentSnapshot?.Hypocenter?.IsVolcano ?? false;
	public string? VolcanoName => CurrentSnapshot?.Hypocenter?.VolcanoName;

	public bool IsCancelled => Fragments.Count > 0 && Fragments.All(x => x.IsCancelled);
	public bool IsTraining => Fragments.Where(x => !x.IsCancelled && !x.IsCorrected).Any(x => x.IsTraining);
	public bool IsTest => Fragments.Where(x => !x.IsCancelled && !x.IsCorrected).Any(x => x.IsTest);

	public string Title => CurrentSnapshot switch
	{
		null => "",
		{ Scope: EarthquakeIntensityScope.HypocenterOnly } => "震源情報",
		{ Scope: EarthquakeIntensityScope.AreaOnly, Hypocenter: not null } => "震度速報+震源情報",
		{ Scope: EarthquakeIntensityScope.AreaOnly } => "震度速報",
		{ Hypocenter.IsVolcano: true } => "大規模噴火",
		{ Hypocenter.IsForeign: true } => "遠地地震情報",
		_ => "震源･震度情報",
	};

	public bool IsVeryShallow => Depth <= 0 && !IsNoDepthData;
	public bool IsNoDepthData => Depth <= -1;
	public bool IsUnknownIntensity => Intensity == JmaIntensity.Unknown;

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
