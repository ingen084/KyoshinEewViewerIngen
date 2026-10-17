using System.Net;

namespace KyoshinEewViewer.UiTests.Infrastructure;

internal sealed class FixtureHttpHandler : HttpMessageHandler
{
	private readonly Dictionary<(HttpMethod, string), Queue<Func<HttpResponseMessage>>> _responses = [];

	public void Register(HttpMethod method, Uri uri, string body, HttpStatusCode status = HttpStatusCode.OK)
		=> Register(method, uri, () => new HttpResponseMessage(status) { Content = new StringContent(body) });

	public void Register(HttpMethod method, Uri uri, byte[] body, HttpStatusCode status = HttpStatusCode.OK)
		=> Register(method, uri, () => new HttpResponseMessage(status) { Content = new ByteArrayContent(body) });

	private void Register(HttpMethod method, Uri uri, Func<HttpResponseMessage> response)
	{
		var key = (method, NetworkAudit.Canonical(uri));
		lock (_responses)
		{
			if (!_responses.TryGetValue(key, out var queue))
				_responses[key] = queue = new();
			queue.Enqueue(response);
		}
	}

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var uri = request.RequestUri ?? throw new InvalidOperationException("HTTP要求先がありません。");
		var key = (request.Method, NetworkAudit.Canonical(uri));
		lock (_responses)
		{
			if (!_responses.TryGetValue(key, out var queue) || queue.Count == 0)
			{
				NetworkAudit.Reject($"{request.Method} {uri}");
				throw new HttpRequestException("未登録の通信要求を拒否しました。");
			}
			NetworkAudit.ApproveFixture(uri);
			return Task.FromResult(queue.Dequeue()());
		}
	}
}
