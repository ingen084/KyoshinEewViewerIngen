using System;
using System.Threading;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services;

public sealed record HttpTimingMetrics(long Count = 0, double LastMs = 0, double TotalMs = 0, double MaxMs = 0)
{
	public double AverageMs => Count == 0 ? 0 : TotalMs / Count;
	internal HttpTimingMetrics Add(double milliseconds) => new(Count + 1, milliseconds, TotalMs + milliseconds, Math.Max(MaxMs, milliseconds));
}

/// <summary>強震モニタの起動後の累積値。取得できなかった段階の時間は集計に含めない。</summary>
public sealed record HttpMetricsSnapshot
{
	public long Completed { get; init; }
	public long InFlight { get; init; }
	public long Succeeded { get; init; }
	public long Failed { get; init; }
	public long HttpErrors { get; init; }
	public long Timeouts { get; init; }
	public long Delayed { get; init; }
	public long ReceivedBytes { get; init; }
	public long ConnectionsOpened { get; init; }
	public long ConnectionsClosed { get; init; }
	public long ActiveConnections => ConnectionsOpened - ConnectionsClosed;
	public long ReusedRequests { get; init; }
	public long TcpSucceeded { get; init; }
	public long TcpFailed { get; init; }
	public long RefillFailed { get; init; }
	public long WarmHits { get; init; }
	public long ColdConnects { get; init; }
	public long WarmDiscarded { get; init; }
	public int? LastStatusCode { get; init; }
	public long? LastConnectionId { get; init; }
	public DateTimeOffset? LastCompletedAt { get; init; }
	public HttpTimingMetrics RequestTime { get; init; } = new();
	public HttpTimingMetrics QueueTime { get; init; } = new();
	public HttpTimingMetrics ResponseWaitTime { get; init; } = new();
	public HttpTimingMetrics BodyTime { get; init; } = new();
	public HttpTimingMetrics TcpConnectTime { get; init; } = new();
}

/// <summary>通信スレッドで数値だけを集計し、UI は不変のスナップショットを読む。</summary>
internal sealed class HttpRequestMetrics
{
	private readonly Lock _lock = new();
	private HttpMetricsSnapshot _snapshot = new();

	internal HttpMetricsSnapshot GetSnapshot() { lock (_lock) return _snapshot; }
	internal void Started() { lock (_lock) _snapshot = _snapshot with { InFlight = _snapshot.InFlight + 1 }; }
	internal void Delayed() { lock (_lock) _snapshot = _snapshot with { Delayed = _snapshot.Delayed + 1 }; }
	internal void ConnectionOpened() { lock (_lock) _snapshot = _snapshot with { ConnectionsOpened = _snapshot.ConnectionsOpened + 1 }; }
	internal void ConnectionClosed() { lock (_lock) _snapshot = _snapshot with { ConnectionsClosed = _snapshot.ConnectionsClosed + 1 }; }
	internal void WarmHit() { lock (_lock) _snapshot = _snapshot with { WarmHits = _snapshot.WarmHits + 1 }; }
	internal void ColdConnect() { lock (_lock) _snapshot = _snapshot with { ColdConnects = _snapshot.ColdConnects + 1 }; }
	internal void WarmDiscarded() { lock (_lock) _snapshot = _snapshot with { WarmDiscarded = _snapshot.WarmDiscarded + 1 }; }

	internal void TcpCompleted(double milliseconds, bool succeeded, bool refill)
	{
		lock (_lock)
			_snapshot = _snapshot with
			{
				TcpSucceeded = _snapshot.TcpSucceeded + (succeeded ? 1 : 0),
				TcpFailed = _snapshot.TcpFailed + (succeeded ? 0 : 1),
				RefillFailed = _snapshot.RefillFailed + (!succeeded && refill ? 1 : 0),
				TcpConnectTime = _snapshot.TcpConnectTime.Add(milliseconds),
			};
	}

	internal void Completed(HttpRequestDiagnostics.Request request, int? statusCode, long bytes, bool succeeded, bool timeout)
	{
		lock (_lock)
			_snapshot = _snapshot with
			{
				Completed = _snapshot.Completed + 1,
				InFlight = _snapshot.InFlight - 1,
				Succeeded = _snapshot.Succeeded + (succeeded ? 1 : 0),
				Failed = _snapshot.Failed + (succeeded ? 0 : 1),
				HttpErrors = _snapshot.HttpErrors + (statusCode.HasValue && !succeeded ? 1 : 0),
				Timeouts = _snapshot.Timeouts + (timeout ? 1 : 0),
				ReceivedBytes = _snapshot.ReceivedBytes + bytes,
				ReusedRequests = _snapshot.ReusedRequests + (request.ReusedConnection ? 1 : 0),
				LastStatusCode = statusCode,
				LastConnectionId = request.ConnectionId,
				LastCompletedAt = DateTimeOffset.Now,
				RequestTime = _snapshot.RequestTime.Add(request.ElapsedMs),
				QueueTime = AddIfPresent(_snapshot.QueueTime, request.QueueMs),
				ResponseWaitTime = AddIfPresent(_snapshot.ResponseWaitTime, request.ResponseWaitMs),
				BodyTime = AddIfPresent(_snapshot.BodyTime, request.BodyMs),
			};
	}

	private static HttpTimingMetrics AddIfPresent(HttpTimingMetrics timing, double? milliseconds)
		=> milliseconds is { } value ? timing.Add(value) : timing;
}
