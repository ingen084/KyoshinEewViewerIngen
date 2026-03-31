using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ReplayGenerator.Infrastructure;
using ReplayGenerator.Models;

namespace ReplayGenerator.Services;

public class ShakeDetectionTracker
{
	private readonly ValkeyStateManager _state;
	private readonly ILogger<ShakeDetectionTracker> _logger;

	private ShakeState? _current;
	private DateTime? _waitUntil;

	public ShakeDetectionTracker(ValkeyStateManager state, ILogger<ShakeDetectionTracker> logger)
	{
		_state = state;
		_logger = logger;
	}

	/// <summary>
	/// 再起動時に Valkey から状態を復元する
	/// </summary>
	public async Task RestoreAsync()
	{
		_current = await _state.LoadShakeState();
		if (_current != null)
			_logger.LogInformation($"揺れ検知セッションを復元しました: {_current.ShakeEventId} (status={_current.Status})");
	}

	/// <summary>
	/// shake_detected メッセージを受信した際の処理
	/// </summary>
	public async Task<bool> OnShakeDetected(string eventId)
	{
		if (_current != null && _current.ShakeEventId == eventId)
		{
			_current.LastEventTime = DateTime.UtcNow;
			_current.Status = SessionStatus.Tracking;
			_waitUntil = null;
			await _state.SaveShakeState(_current);
			return false;
		}

		if (_current != null)
			return false;

		var acquired = await _state.TryAcquireLock("shake", eventId, TimeSpan.FromMinutes(10));
		if (!acquired)
			return false;

		_current = new ShakeState
		{
			ShakeEventId = eventId,
			StartTime = DateTime.UtcNow,
			LastEventTime = DateTime.UtcNow,
			Status = SessionStatus.Tracking,
		};
		await _state.SaveShakeState(_current);
		_logger.LogInformation($"揺れ検知トラッキング開始: {eventId}");
		return true;
	}

	/// <summary>
	/// EEW 情報を関連付ける
	/// </summary>
	public async Task SetEewSnapshot(string eewJson)
	{
		if (_current == null) return;
		_current.EewJson = eewJson;
		await _state.SaveShakeState(_current);
	}

	/// <summary>
	/// 1秒ごとのタイマーで揺れ終了を判定する。
	/// 戻り値が true の場合はリプレイファイル生成を開始する。
	/// </summary>
	public async Task<(bool ShouldGenerate, ShakeState? State)> CheckTimerAsync(string? snapshotJson)
	{
		if (_current == null)
			return (false, null);

		if (_current.Status == SessionStatus.Tracking)
		{
			var elapsed = DateTime.UtcNow - _current.LastEventTime;
			if (elapsed < TimeSpan.FromSeconds(30))
				return (false, null);

			_current.Status = SessionStatus.Waiting;

			var waitSeconds = DetermineWaitSeconds(snapshotJson);
			_waitUntil = DateTime.UtcNow.AddSeconds(waitSeconds);
			_logger.LogInformation($"揺れ終了検知、{waitSeconds}秒待機開始: {_current.ShakeEventId}");
			await _state.SaveShakeState(_current);
			return (false, null);
		}

		if (_current.Status == SessionStatus.Waiting && _waitUntil.HasValue && DateTime.UtcNow >= _waitUntil.Value)
		{
			_current.Status = SessionStatus.Generating;
			await _state.SaveShakeState(_current);
			return (true, _current);
		}

		return (false, null);
	}

	/// <summary>
	/// 生成完了後のクリーンアップ
	/// </summary>
	public async Task CompleteAsync()
	{
		if (_current == null) return;
		await _state.ReleaseLock("shake", _current.ShakeEventId);
		await _state.ClearShakeState();
		_current = null;
		_waitUntil = null;
	}

	private int DetermineWaitSeconds(string? snapshotJson)
	{
		if (snapshotJson == null) return 30;

		try
		{
			using var doc = JsonDocument.Parse(snapshotJson);
			if (!doc.RootElement.TryGetProperty("eews", out var eews))
				return 30;

			foreach (var eew in eews.EnumerateArray())
			{
				if (eew.TryGetProperty("hypocenter", out var hypo) && hypo.TryGetProperty("depth", out var depthProp))
				{
					var depth = depthProp.GetInt32();
					var magnitude = eew.TryGetProperty("magnitude", out var magProp) ? magProp.GetDouble() : 0;

					if (depth <= 150 && magnitude <= 5)
						return 60;
					return 180;
				}
			}
		}
		catch { }

		return 30;
	}
}
