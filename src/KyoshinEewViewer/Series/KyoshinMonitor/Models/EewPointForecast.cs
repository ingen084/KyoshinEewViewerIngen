using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinMonitorLib;
using System;
using System.Globalization;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Models;

/// <summary>
/// 外部アプリが算出した地点ごとの予測
/// カウントダウンを行の増減なしで更新するため、record ではなく ReactiveObject とする
/// </summary>
public class EewPointForecast : ObservableObject
{
	/// <summary>
	/// 紐づく EEW の地震ID
	/// </summary>
	public required string EventId { get; init; }

	/// <summary>
	/// 提供元の識別子 上書きのキーに使用する
	/// </summary>
	public required string SourceKey { get; init; }

	/// <summary>
	/// 表示に使用する提供元名
	/// </summary>
	public required string DisplaySource { get; init; }

	/// <summary>
	/// 紐づく EEW の報数
	/// </summary>
	public required int SerialNo { get; init; }

	/// <summary>
	/// 地点名
	/// </summary>
	public required string PointName { get; init; }

	/// <summary>
	/// 地点の座標 提供されていない場合は地図に表示しない
	/// </summary>
	public Location? Location { get; init; }

	/// <summary>
	/// 提供元が算出した計測震度
	/// </summary>
	public required double RealtimeIntensity { get; init; }

	/// <summary>
	/// 計測震度から求めた震度階級
	/// </summary>
	public JmaIntensity Intensity => RealtimeIntensity.ToJmaIntensity();

	/// <summary>
	/// 提供元が算出した揺れの到達時刻
	/// KEVi では補正を行わない
	/// </summary>
	public DateTime? ArrivalTime { get; init; }

	/// <summary>
	/// 紐づく EEW の地震発生時刻
	/// </summary>
	public DateTime? OccurrenceTime { get; init; }

	/// <summary>
	/// この地点予測を受け取った時刻 鮮度の表示に使用する
	/// </summary>
	public required DateTime ReceiveTime { get; init; }

	private double? _remainingSeconds;
	/// <summary>
	/// 揺れの到達までの残り秒数 到達時刻が提供されていない場合は null
	/// </summary>
	public double? RemainingSeconds
	{
		get => _remainingSeconds;
		private set => SetProperty(ref _remainingSeconds, value);
	}

	private bool _isArrived;
	/// <summary>
	/// 揺れが到達済みか
	/// </summary>
	public bool IsArrived
	{
		get => _isArrived;
		private set => SetProperty(ref _isArrived, value);
	}

	private double? _elapsedSecondsSinceArrival;
	/// <summary>
	/// 到達からの経過秒数 未到達の場合は null
	/// </summary>
	public double? ElapsedSecondsSinceArrival
	{
		get => _elapsedSecondsSinceArrival;
		private set => SetProperty(ref _elapsedSecondsSinceArrival, value);
	}

	private int _elapsedSecondsSinceReceive;
	/// <summary>
	/// 受信からの経過秒数 値の鮮度として表示する
	/// </summary>
	public int ElapsedSecondsSinceReceive
	{
		get => _elapsedSecondsSinceReceive;
		private set => SetProperty(ref _elapsedSecondsSinceReceive, value);
	}

	private bool _isExpanded;
	/// <summary>
	/// 大きなカウントダウンに展開表示するか
	/// 行の増減を発生させないため、プロパティの切り替えで表現する
	/// </summary>
	public bool IsExpanded
	{
		get => _isExpanded;
		private set => SetProperty(ref _isExpanded, value);
	}

	/// <summary>
	/// 計測震度の表示用文字列
	/// </summary>
	public string RealtimeIntensityText => RealtimeIntensity.ToString("0.0", CultureInfo.InvariantCulture);

	private string _sourceText = "";
	/// <summary>
	/// 提供元・報数・鮮度の表示用文字列
	/// </summary>
	public string SourceText
	{
		get => _sourceText;
		private set => SetProperty(ref _sourceText, value);
	}

	private string _countdownText = "";
	/// <summary>
	/// 折りたたみ行のカウントダウン表示 1秒刻み
	/// </summary>
	public string CountdownText
	{
		get => _countdownText;
		private set => SetProperty(ref _countdownText, value);
	}

	private string _fineCountdownText = "";
	/// <summary>
	/// 展開行のカウントダウン表示 0.1秒刻み
	/// </summary>
	public string FineCountdownText
	{
		get => _fineCountdownText;
		private set => SetProperty(ref _fineCountdownText, value);
	}

	private double _progressPercent;
	/// <summary>
	/// 到達までの進捗 0〜100
	/// </summary>
	public double ProgressPercent
	{
		get => _progressPercent;
		private set => SetProperty(ref _progressPercent, value);
	}

	/// <summary>
	/// 到達済みの表示を継続する秒数 これを超えると展開表示を解除する
	/// </summary>
	private const double KeepExpandedSecondsAfterArrival = 5;

	/// <summary>
	/// 現在時刻に応じて表示用のプロパティを更新する
	/// プロパティ変更通知を伴うため UI スレッドから呼び出すこと
	/// </summary>
	/// <param name="currentTime">同期済みの現在時刻</param>
	/// <param name="canExpand">展開表示を行うか(設定と同一地点内での優先判定の結果)</param>
	public void UpdateDisplayValues(DateTime currentTime, bool canExpand)
	{
		// 時刻の逆行に備えて負の値は0として扱う
		ElapsedSecondsSinceReceive = Math.Max(0, (int)(currentTime - ReceiveTime).TotalSeconds);
		SourceText = $"{DisplaySource} ({SerialNo:00}報・{ElapsedSecondsSinceReceive}秒前)";

		if (ArrivalTime is not { } arrivalTime)
		{
			RemainingSeconds = null;
			IsArrived = false;
			ElapsedSecondsSinceArrival = null;
			ProgressPercent = 0;
			// 震度0の場合は揺れが到達しない旨を表示する
			CountdownText = Intensity <= JmaIntensity.Int0 ? "揺れの\n心配なし" : "到達時刻\n提供なし";
			FineCountdownText = "";
			// 到達時刻が提供されていない場合はカウントダウンを表示できないため展開しない
			IsExpanded = false;
			return;
		}

		var remaining = (arrivalTime - currentTime).TotalSeconds;
		// 進捗は地震発生から到達までを100%として求める 発生時刻が不明な場合は受信時点を起点にする
		var leadSeconds = (arrivalTime - (OccurrenceTime ?? ReceiveTime)).TotalSeconds;
		ProgressPercent = leadSeconds > 0 ? Math.Clamp((1 - remaining / leadSeconds) * 100, 0, 100) : 100;

		if (remaining > 0)
		{
			RemainingSeconds = remaining;
			IsArrived = false;
			ElapsedSecondsSinceArrival = null;
			// 折りたたみ行は0秒で数え終わらないよう切り上げる
			CountdownText = $"{Math.Ceiling(remaining):0} 秒";
			FineCountdownText = remaining.ToString("0.0", CultureInfo.InvariantCulture);
			IsExpanded = canExpand;
			return;
		}

		var elapsed = -remaining;
		RemainingSeconds = 0;
		IsArrived = true;
		ElapsedSecondsSinceArrival = elapsed;
		CountdownText = $"到達済み\n+{Math.Floor(elapsed):0}秒";
		FineCountdownText = $"+{elapsed.ToString("0.0", CultureInfo.InvariantCulture)}";
		// 到達直後のみ展開表示を継続する
		IsExpanded = canExpand && elapsed <= KeepExpandedSecondsAfterArrival;
	}
}
