using System;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services;

/// <summary>
/// <see cref="WarmSocketPool"/> の動作パラメータ。
/// </summary>
public sealed class WarmSocketPoolOptions
{
	/// <summary>
	/// ソケットの最大保持時間の初期値。
	/// <see cref="WarmSocketPool.UpdateMaxAge"/> で動的に変更されない限り、この値が使われ続ける
	/// </summary>
	public TimeSpan InitialMaxAge { get; init; } = TimeSpan.FromSeconds(55);

	/// <summary>
	/// 1 回の TCP connect の制限時間
	/// </summary>
	public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(4.9);

	/// <summary>
	/// メンテナンスタスクのチェック間隔
	/// </summary>
	public TimeSpan MaintenanceInterval { get; init; } = TimeSpan.FromSeconds(5);
}
