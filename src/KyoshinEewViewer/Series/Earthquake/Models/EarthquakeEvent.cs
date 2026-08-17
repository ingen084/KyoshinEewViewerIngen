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

public partial class EarthquakeEvent : ObservableObject
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

	/// <summary>
	/// 該当項目が選択中か
	/// </summary>
	[ObservableProperty]
	public partial bool IsSelecting { get; set; }

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

	/// <summary>
	/// イベントのタイトル(現在の情報種別)
	/// </summary>
	[ObservableProperty]
	public partial string? Title { get; private set; }

	/// <summary>
	/// 補足情報(存在する場合は外部から設定する)
	/// </summary>
	[ObservableProperty]
	public partial string? Subtitle { get; set; }

	/// <summary>
	/// 最新の電文の発表時刻
	/// </summary>
	[ObservableProperty]
	public partial DateTime UpdatedTime { get; set; }

	/// <summary>
	/// 震度速報
	/// </summary>
	[ObservableProperty]
	public partial bool IsSokuhou { get; set; }

	/// <summary>
	/// 遠地地震
	/// </summary>
	[ObservableProperty]
	public partial bool IsForeign { get; set; }

	/// <summary>
	/// 火山噴火
	/// </summary>
	[ObservableProperty]
	public partial bool IsVolcano { get; set; }

	/// <summary>
	/// 火山名
	/// </summary>
	[ObservableProperty]
	public partial string? VolcanoName { get; set; }

	/// <summary>
	/// 震度速報かつ最大震度の観測が1地域のみ
	/// </summary>
	[ObservableProperty]
	public partial bool IsOnlypoint { get; set; }

	/// <summary>
	/// 訓練
	/// </summary>
	[ObservableProperty]
	public partial bool IsTraining { get; set; }

	/// <summary>
	/// 試験
	/// </summary>
	[ObservableProperty]
	public partial bool IsTest { get; set; }

	/// <summary>
	/// 震源のみ
	/// </summary>
	[ObservableProperty]
	public partial bool IsHypocenterOnly { get; set; }

	/// <summary>
	/// 震源震度情報を適用済み<br/>これ以降は震度速報は震度情報のみ更新する
	/// </summary>
	[ObservableProperty]
	public partial bool IsDetailIntensityApplied { get; set; }

	/// <summary>
	/// 属しているすべての電文(=該当イベントID)がキャンセル扱いになっている
	/// </summary>
	[ObservableProperty]
	public partial bool IsCancelled { get; set; }

	/// <summary>
	/// 発生もしくは検知時刻
	/// </summary>
	[ObservableProperty]
	public partial DateTime Time { get; set; }

	/// <summary>
	/// 時刻は検知時刻を示しているか
	/// </summary>
	[ObservableProperty]
	public partial bool IsDetectionTime { get; set; }

	/// <summary>
	/// 震央地名もしくは観測地名(震度速報)
	/// </summary>
	[ObservableProperty]
	public partial string? Place { get; set; }

	/// <summary>
	/// 震央座標
	/// </summary>
	[ObservableProperty]
	public partial Location? Location { get; set; }

	/// <summary>
	/// 震央座標の誤差 (±度)
	/// </summary>
	[ObservableProperty]
	public partial Location? LocationError { get; set; }

	/// <summary>
	/// 最大震度
	/// </summary>
	[ObservableProperty]
	public partial JmaIntensity Intensity { get; set; } = JmaIntensity.Unknown;

	/// <summary>
	/// 最大の長周期地震動階級
	/// </summary>
	[ObservableProperty]
	public partial LpgmIntensity? LpgmIntensity { get; set; }

	/// <summary>
	/// 規模
	/// </summary>
	[ObservableProperty]
	public partial float Magnitude { get; set; }

	/// <summary>
	/// 規模の代替テキスト
	/// </summary>
	[ObservableProperty]
	public partial string? MagnitudeAlternativeText { get; set; }

	/// <summary>
	/// 深さ(km)
	/// </summary>
	[ObservableProperty]
	public partial int Depth { get; set; } = -1;

	/// <summary>
	/// 深さの誤差 (±km)
	/// </summary>
	[ObservableProperty]
	public partial int? DepthError { get; set; }

	/// <summary>
	/// コメント
	/// </summary>
	[ObservableProperty]
	public partial string? Comment { get; set; }

	/// <summary>
	/// 自由形式のコメント
	/// </summary>
	[ObservableProperty]
	public partial string? FreeFormComment { get; set; }

	[ObservableProperty]
	public partial bool IsHypocenterAvailable { get; private set; }

	[ObservableProperty]
	public partial bool IsVeryShallow { get; private set; }

	[ObservableProperty]
	public partial bool IsNoDepthData { get; private set; }

	[ObservableProperty]
	public partial bool IsUnknownIntensity { get; private set; }

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
