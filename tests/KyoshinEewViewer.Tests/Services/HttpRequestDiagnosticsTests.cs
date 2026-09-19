using KyoshinEewViewer.Series.KyoshinMonitor.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace KyoshinEewViewer.Tests.Services;

public sealed class HttpRequestDiagnosticsTests
{
	[Fact]
	public void SuccessfulAndDelayedRequestsAreCountedOnceWithoutLogs()
	{
		var logger = new RecordingLogger();
		using var diagnostics = new HttpRequestDiagnostics(logger);
		using (var request = diagnostics.Begin("http://localhost/image.gif", TimeSpan.FromSeconds(10)))
		{
			Assert.Equal(1, diagnostics.Metrics.GetSnapshot().InFlight);
			request.MarkDelayed();
			request.MarkDelayed();
			using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("test") };
			request.Complete(response);
			request.Complete(response);
		}
		var snapshot = diagnostics.Metrics.GetSnapshot();
		Assert.Equal(1, snapshot.Completed);
		Assert.Equal(1, snapshot.Succeeded);
		Assert.Equal(1, snapshot.Delayed);
		Assert.Equal(4, snapshot.ReceivedBytes);
		Assert.Equal(0, snapshot.InFlight);
		Assert.Equal(0, snapshot.Failed);
		Assert.Equal(0, snapshot.BodyTime.Count); // 計測していない区間を 0ms として集計しない
		Assert.Empty(logger.Messages);
	}

	[Fact]
	public void HttpErrorsAreLoggedWithoutQueryAndCountedSeparatelyFromTimeouts()
	{
		var logger = new RecordingLogger();
		using var diagnostics = new HttpRequestDiagnostics(logger);
		using (var request = diagnostics.Begin("http://localhost/image.gif?token=secret", TimeSpan.FromSeconds(10)))
		{
			using var response = new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("missing") };
			request.Complete(response);
		}
		var snapshot = diagnostics.Metrics.GetSnapshot();
		Assert.Equal(1, snapshot.Failed);
		Assert.Equal(1, snapshot.HttpErrors);
		Assert.Equal(0, snapshot.Timeouts);
		Assert.Equal(404, snapshot.LastStatusCode);
		var message = Assert.Single(logger.Messages);
		Assert.Contains("HTTP 404", message);
		Assert.DoesNotContain("secret", message);
	}

	[Fact]
	public async Task RealHttpConnectionIsReusedAndUnrelatedRequestsAreIgnored()
	{
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/test";
		var server = Task.Run(async () =>
		{
			using var socket = await listener.AcceptSocketAsync(deadline.Token);
			using var stream = new NetworkStream(socket);
			using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
			for (var i = 0; i < 3; i++)
			{
				await ReadRequestAsync(reader, deadline.Token);
				await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\ntest"u8.ToArray(), deadline.Token);
			}
		}, deadline.Token);
		var logger = new RecordingLogger(LogLevel.Warning);
		using var diagnostics = new HttpRequestDiagnostics(logger);
		using (var client = new HttpClient(new SocketsHttpHandler { UseProxy = false }))
		{
			for (var i = 0; i < 2; i++)
			{
				using var request = diagnostics.Begin(url, TimeSpan.FromSeconds(10));
				using var response = await client.GetAsync(url, deadline.Token);
				request.Complete(response);
			}
			using var unrelated = await client.GetAsync(url, deadline.Token);
		}
		await server;
		var snapshot = diagnostics.Metrics.GetSnapshot();
		Assert.Equal(2, snapshot.Completed);
		Assert.Equal(2, snapshot.Succeeded);
		Assert.Equal(1, snapshot.ConnectionsOpened);
		Assert.Equal(1, snapshot.ConnectionsClosed);
		Assert.Equal(0, snapshot.ActiveConnections);
		Assert.Equal(1, snapshot.ReusedRequests);
		Assert.Equal(2, snapshot.ResponseWaitTime.Count);
		Assert.Equal(2, snapshot.BodyTime.Count);
		Assert.Empty(logger.Messages);
	}

	[Theory]
	[InlineData(false, "RequestHeadersStop")]
	[InlineData(true, "ResponseContentStart")]
	public async Task TimeoutPreservesFailedPhaseAndDoesNotCountIncompleteBody(bool partialBody, string expectedPhase)
	{
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var url = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/test";
		var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var server = Task.Run(async () =>
		{
			using var socket = await listener.AcceptSocketAsync(deadline.Token);
			using var stream = new NetworkStream(socket);
			using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
			await ReadRequestAsync(reader, deadline.Token);
			if (partialBody)
				await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 100\r\n\r\nx"u8.ToArray(), deadline.Token);
			ready.SetResult();
			await release.Task.WaitAsync(deadline.Token);
		}, deadline.Token);
		var logger = new RecordingLogger();
		using var diagnostics = new HttpRequestDiagnostics(logger);
		using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false });
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
		try
		{
			using var request = diagnostics.Begin(url, TimeSpan.FromMilliseconds(200));
			var responseTask = client.GetAsync(url, cancellation.Token);
			await ready.Task.WaitAsync(deadline.Token);
			cancellation.CancelAfter(TimeSpan.FromMilliseconds(200));
			var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => responseTask);
			request.Fail(exception, true);
			Assert.Equal(expectedPhase, request.Phase);
		}
		finally { release.TrySetResult(); }
		await server;
		var snapshot = diagnostics.Metrics.GetSnapshot();
		Assert.Equal(1, snapshot.Completed);
		Assert.Equal(1, snapshot.Failed);
		Assert.Equal(1, snapshot.Timeouts);
		Assert.Equal(0, snapshot.HttpErrors);
		Assert.Equal(0, snapshot.InFlight);
		Assert.Equal(0, snapshot.BodyTime.Count);
		Assert.Contains($"lastPhase={expectedPhase}", Assert.Single(logger.Messages));
	}

	[Fact]
	public async Task ConcurrentRequestsKeepAccurateTotalsAndIndependentScopes()
	{
		using var diagnostics = new HttpRequestDiagnostics(new RecordingLogger());
		await Task.WhenAll(Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
		{
			using var request = diagnostics.Begin("http://localhost/test", TimeSpan.FromSeconds(1));
			using var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("x") };
			request.Complete(response);
		})));
		var snapshot = diagnostics.Metrics.GetSnapshot();
		Assert.Equal(100, snapshot.Completed);
		Assert.Equal(100, snapshot.Succeeded);
		Assert.Equal(100, snapshot.ReceivedBytes);
		Assert.Equal(0, snapshot.InFlight);
		Assert.Null(HttpRequestDiagnostics.CurrentRequestId);
	}

	private static async Task ReadRequestAsync(StreamReader reader, CancellationToken ct)
	{
		while (await reader.ReadLineAsync(ct) is { } line)
			if (line.Length == 0) return;
		throw new IOException("Request ended before headers completed");
	}

	private sealed class RecordingLogger(LogLevel minimum = LogLevel.Trace) : ILogger
	{
		internal ConcurrentQueue<string> Messages { get; } = new();
		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
		public bool IsEnabled(LogLevel logLevel) => logLevel >= minimum;
		public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			if (IsEnabled(level)) Messages.Enqueue(formatter(state, exception));
		}
	}
}
