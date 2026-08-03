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

	/// <summary>
	/// TCP keepalive プローブを送り始めるまでの無通信時間。
	/// NAT のセッションテーブル維持とサイレント切断の検知が目的のため、
	/// MAP-E などタイムアウトが短い環境の NAT アイドルタイムアウトより十分短くする
	/// </summary>
	public TimeSpan KeepAliveTime { get; init; } = TimeSpan.FromSeconds(15);

	/// <summary>
	/// TCP keepalive プローブの再送間隔
	/// </summary>
	public TimeSpan KeepAliveInterval { get; init; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// TCP keepalive プローブの再送回数 (すべて無応答だとソケットが切断扱いになる)
	/// </summary>
	public int KeepAliveRetryCount { get; init; } = 2;
}
