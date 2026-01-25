using KyoshinMonitorLib;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Models;

public record Eew
{
	/// <summary>
	/// 地震ID
	/// </summary>
	public required string Id { get; init; }

	/// <summary>
	/// EEWの情報元
	/// </summary>
	public required EewSource Source { get; init; }

	/// <summary>
	/// 表示する情報元
	/// </summary>
	public required string DisplaySource { get; init; }

	/// <summary>
	/// キャンセル報か 受信範囲外の可能性もある
	/// </summary>
	public bool IsCancelled { get; init; }

	/// <summary>
	/// 確実にキャンセル報であることがわかっているか
	/// </summary>
	public bool IsTrueCancelled { get; init; }

	/// <summary>
	/// 受信時刻
	/// </summary>
	public required DateTime ReceiveTime { get; init; }

	/// <summary>
	/// 報数
	/// </summary>
	public required int SerialNo { get; init; }

	/// <summary>
	/// 最終報か
	/// </summary>
	public required bool IsFinal { get; init; }

	/// <summary>
	/// 最大震度
	/// </summary>
	public JmaIntensity MaxIntensity { get; init; } = JmaIntensity.Unknown;

	/// <summary>
	/// 最大震度に 以上 がつくかどうか
	/// </summary>
	public bool IsIntensityOver { get; init; }

	/// <summary>
	/// 最大長周期地震動階級
	/// </summary>
	// public LpgmIntensity LpgmIntensity { get; init; } = LpgmIntensity.Unknown;

	/// <summary>
	/// 震源情報
	/// </summary>
	public required EewHypocenter? Hypocenter { get; init; }

	/// <summary>
	/// 予想震度一覧
	/// </summary>
	public Dictionary<int, JmaIntensity>? IntensityForecastMap { get; init; }

	/// <summary>
	/// 警報地域の一覧
	/// </summary>
	public EewWarningAreas? WarningAreas { get; init; }

	/// <summary>
	/// 警報状態か
	/// </summary>
	public required bool IsWarning { get; init; }
}

public enum EewSource
{
	KyoshinMonitor,
	SignalNowProfessional,
	Dmdata,
	Axis,
}

public record EewWarningAreas
{
	/// <summary>
	/// 警報地域のソース
	/// </summary>
	public required string DisplaySource { get; init; }

	/// <summary>
	/// 報数
	/// </summary>
	public required int SerialNo { get; init; }

	/// <summary>
	/// 警報地域コード
	/// </summary>
	public required int[] Codes { get; init; }

	/// <summary>
	/// 警報地域名
	/// </summary>
	public required string[] Names { get; init; }

	/// <summary>
	/// 警報電文か？
	/// </summary>
	public bool IsWarningTelegram { get; init; }
}

public record EewHypocenter
{
	/// <summary>
	/// 地震の発生時間
	/// </summary>
	public required DateTime OccurrenceTime { get; init; }

	/// <summary>
	/// 震央地名
	/// </summary>
	public required string? Place { get; init; }

	/// <summary>
	/// 震央座標
	/// </summary>
	public required Location? Location { get; init; }

	/// <summary>
	/// マグニチュード
	/// </summary>
	public required float? Magnitude { get; init; }

	/// <summary>
	/// 震源の深さ
	/// </summary>
	public required int Depth { get; init; }

	/// <summary>
	/// 仮定震源要素か？
	/// </summary>
	public required bool IsTemporary { get; init; }

	/// <summary>
	/// 精度情報
	/// </summary>
	public EewHypocenterAccuracy? Accuracy { get; init; }
}

public record EewHypocenterAccuracy
{
	/// <summary>
	/// 震源要素が最終報レベルの精度か
	/// </summary>
	public required bool IsLocked { get; init; }

	/// <summary>
	/// 震央の確からしさフラグ
	/// </summary>
	public required int LocationAccuracy { get; init; }

	/// <summary>
	/// 深さの確からしさフラグ
	/// </summary>
	public required int DepthAccuracy { get; init; }

	/// <summary>
	/// マグニチュードの確からしさフラグ
	/// </summary>
	public required int MagnitudeAccuracy { get; init; }
}
