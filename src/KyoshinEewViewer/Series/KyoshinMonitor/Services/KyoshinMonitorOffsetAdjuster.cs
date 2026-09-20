using KyoshinEewViewer.Core.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services;

/// <summary>
/// 画像取得時のオフセット調整。短縮に失敗した場合は次の試行までの間隔を延ばす。
/// </summary>
internal sealed class KyoshinMonitorOffsetAdjuster(TimeProvider? timeProvider = null)
{
	private static readonly TimeSpan NormalInterval = TimeSpan.FromMinutes(1);
	private static readonly TimeSpan MaximumInterval = TimeSpan.FromMinutes(30);
	private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
	private TimeSpan _shorteningInterval = NormalInterval;
	private long _lastAdjustmentTimestamp;
	private int? _expectedOffset;
	private bool _automaticAdjustmentEnabled;
	private bool _shorteningPending;

	public async Task<HttpResponseMessage> FetchAsync(
		KyoshinEewViewerConfiguration.TimerConfig timer, Func<Task<HttpResponseMessage>> fetch)
	{
		SynchronizeSettings(timer);
		var offset = timer.Offset;
		HttpResponseMessage response;
		try
		{
			response = await fetch();
		}
		catch
		{
			_lastAdjustmentTimestamp = _timeProvider.GetTimestamp();
			throw;
		}
		// 取得中に手動変更された設定は上書きしない。
		if (!timer.AutoOffsetIncrement || timer.Offset != offset)
		{
			SynchronizeSettings(timer);
			return response;
		}

		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			if (_shorteningPending)
				_shorteningInterval = TimeSpan.FromTicks(Math.Min(MaximumInterval.Ticks, _shorteningInterval.Ticks * 2));
			_shorteningPending = false;
			_lastAdjustmentTimestamp = _timeProvider.GetTimestamp();
			timer.Offset = Math.Min(5000, offset + 100);
			_expectedOffset = timer.Offset;

			// 次の要求を始める前に応答を破棄し、同時に要求を増やさない。
			response.Dispose();
			await Task.Delay(100);
			// 短縮が成立しなくても、その秒の画像を取得して表示の欠落を防ぐ。
			// 再取得では追加の調整や短縮を行わない。
			return await fetch();
		}

		if (response.StatusCode == HttpStatusCode.OK)
		{
			var elapsed = _timeProvider.GetElapsedTime(_lastAdjustmentTimestamp);
			// 短縮後に1分間取得が安定したら通常の間隔へ戻す。
			// 元のオフセットへ戻した後の成功では待機を解除しない。
			if (_shorteningPending && elapsed >= NormalInterval)
			{
				_shorteningInterval = NormalInterval;
				_shorteningPending = false;
			}
			if (offset > 1100 && elapsed >= _shorteningInterval)
			{
				timer.Offset = Math.Max(1100, offset - 100);
				_expectedOffset = timer.Offset;
				_shorteningPending = true;
				_lastAdjustmentTimestamp = _timeProvider.GetTimestamp();
			}
		}
		else
			// HTTPエラーが続いている間は短縮の試行を先送りする。
			_lastAdjustmentTimestamp = _timeProvider.GetTimestamp();
		return response;
	}

	private void SynchronizeSettings(KyoshinEewViewerConfiguration.TimerConfig timer)
	{
		if (_expectedOffset == timer.Offset && _automaticAdjustmentEnabled == timer.AutoOffsetIncrement)
			return;
		_expectedOffset = timer.Offset;
		_automaticAdjustmentEnabled = timer.AutoOffsetIncrement;
		_shorteningInterval = NormalInterval;
		_shorteningPending = false;
		_lastAdjustmentTimestamp = _timeProvider.GetTimestamp();
	}
}
