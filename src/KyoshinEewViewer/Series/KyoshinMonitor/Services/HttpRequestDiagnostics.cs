using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net.Http;
using System.Threading;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services;

/// <summary>強震モニタの要求だけを集計する。ログレベルやデバッグウィンドウの開閉には依存しない。</summary>
internal sealed class HttpRequestDiagnostics(ILogger logger) : EventListener
{
	private static readonly AsyncLocal<Request?> Current = new();
	private static long _nextRequestId;
	private readonly ConcurrentDictionary<long, int> _connections = new();
	internal HttpRequestMetrics Metrics { get; } = new();
	internal static long? CurrentRequestId => Current.Value?.Id;

	internal Request Begin(string url, TimeSpan timeout)
	{
		var request = new Request(this, logger, Current.Value, Interlocked.Increment(ref _nextRequestId), url, timeout);
		Current.Value = request;
		Metrics.Started();
		return request;
	}

	protected override void OnEventSourceCreated(EventSource source)
	{
		if (source.Name == "System.Net.Http")
			EnableEvents(source, EventLevel.Informational);
	}

	protected override void OnEventWritten(EventWrittenEventArgs data)
	{
		// EventListener の基底コンストラクタからも通知され得る。
		if (_connections is null || Metrics is null) return;
		var connectionId = Payload(data, "connectionId") as long?;
		if (data.EventName == "ConnectionClosed")
		{
			if (connectionId is { } closed && _connections.TryRemove(closed, out _))
				Metrics.ConnectionClosed();
			return;
		}
		var request = Current.Value;
		if (request is null || request.Owner != this) return;
		if (connectionId is { } connected && data.EventName == "ConnectionEstablished" && _connections.TryAdd(connected, 0))
			Metrics.ConnectionOpened();
		// 接続を作った要求と、実際にその接続を使う要求が異なる場合もある。
		if (connectionId is { } used && data.EventName == "RequestHeadersStart")
		{
			request.ConnectionId = used;
			request.ReusedConnection = _connections.AddOrUpdate(used, 1, (_, count) => count + 1) > 1;
		}
		request.RecordEvent(data.EventName, Payload(data, "timeOnQueueMilliseconds") as double?);
	}

	private static object? Payload(EventWrittenEventArgs data, string name)
	{
		var index = data.PayloadNames?.IndexOf(name) ?? -1;
		return index < 0 ? null : data.Payload?[index];
	}

	internal sealed class Request(HttpRequestDiagnostics owner, ILogger logger, Request? previous, long id, string url, TimeSpan timeout) : IDisposable
	{
		private readonly long _started = Stopwatch.GetTimestamp();
		private bool _completed, _failed, _delayed;
		private double? _headersSentAt, _bodyStartedAt;
		internal HttpRequestDiagnostics Owner => owner;
		internal long Id { get; } = id;
		internal long? ConnectionId { get; set; }
		internal bool ReusedConnection { get; set; }
		internal string Phase { get; private set; } = "RequestStart";
		internal double ElapsedMs => Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
		internal double? QueueMs { get; private set; }
		internal double? ResponseWaitMs { get; private set; }
		internal double? BodyMs { get; private set; }

		internal void RecordEvent(string? name, double? queueMilliseconds = null)
		{
			if (name == "RequestFailed") _failed = true;
			// finally の ResponseContentStop を成功した本文受信と誤認しない。
			if (_failed || _completed) return;
			var elapsed = ElapsedMs;
			switch (name)
			{
				case "RequestLeftQueue": QueueMs = queueMilliseconds; break;
				case "RequestHeadersStart": _headersSentAt = null; _bodyStartedAt = null; ResponseWaitMs = null; BodyMs = null; break;
				case "RequestHeadersStop": _headersSentAt = elapsed; break;
				case "ResponseHeadersStart": ResponseWaitMs = elapsed - _headersSentAt; break;
				case "ResponseHeadersStop": break;
				case "ResponseContentStart": _bodyStartedAt = elapsed; break;
				case "ResponseContentStop": BodyMs = elapsed - _bodyStartedAt; break;
				default: return;
			}
			Phase = name;
		}

		internal void MarkDelayed()
		{
			if (_delayed || _completed) return;
			_delayed = true;
			owner.Metrics.Delayed();
		}

		internal void Complete(HttpResponseMessage response)
		{
			if (_completed) return;
			_completed = true;
			owner.Metrics.Completed(this, (int)response.StatusCode, response.Content.Headers.ContentLength ?? 0, response.IsSuccessStatusCode, false);
			if (!response.IsSuccessStatusCode) LogFailure($"HTTP {(int)response.StatusCode}", false);
		}

		internal void Fail(Exception exception, bool requestTimeout)
		{
			if (_completed) return;
			_completed = true;
			owner.Metrics.Completed(this, null, 0, false, exception is OperationCanceledException);
			LogFailure($"{exception.GetType().Name}: {exception.Message}", requestTimeout);
		}

		private void LogFailure(string reason, bool requestTimeout) => logger.LogWarning(
			"HTTP取得失敗 request={RequestId} connection={ConnectionId} elapsedMs={ElapsedMs:F1} lastPhase={Phase} timeoutMs={TimeoutMs} requestTimeout={RequestTimeout} url={Url} error={Error}",
			Id, ConnectionId, ElapsedMs, Phase, timeout.TotalMilliseconds, requestTimeout, new Uri(url).GetLeftPart(UriPartial.Path), reason);

		public void Dispose()
		{
			// 呼び出し側の処理が途中で抜けた場合にも実行中の件数を残さない。
			if (!_completed) Fail(new InvalidOperationException("要求の計測が完了前に終了しました。"), false);
			Current.Value = previous;
		}
	}
}
