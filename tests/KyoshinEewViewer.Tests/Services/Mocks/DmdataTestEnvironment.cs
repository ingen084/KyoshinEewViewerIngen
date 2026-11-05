using System.Net;
using DmdataSharp;
using DmdataSharp.Authentication.OAuth;
using DmdataSharp.Interfaces;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using Microsoft.Extensions.Logging.Abstractions;

namespace KyoshinEewViewer.Tests.Services.Mocks;

/// <summary>
/// 実際の通信を行わずに DmdataRedundantTelegramPublisher を生成する
/// </summary>
public static class DmdataTestEnvironment
{
	// 名前解決できない改変ホスト名
	public const string ApiBaseUrl = "https://apidmdatajp";
	public const string DataApiBaseUrl = "https://dataapidmdatajp";

	public static DmdataRedundantTelegramPublisher CreatePublisher(
		KyoshinEewViewerConfiguration config,
		IRedundantDmdataSocketController socketController,
		StubHttpHandler? http = null,
		FakeOAuthCredential? credential = null)
	{
		config.Dmdata.ApiBaseUrl = ApiBaseUrl;
		config.Dmdata.DataApiBaseUrl = DataApiBaseUrl;
		var client = new HttpClient(http ?? new StubHttpHandler());
		return new DmdataRedundantTelegramPublisher(
			NullLogger<DmdataRedundantTelegramPublisher>.Instance,
			config,
			new InformationCacheService(NullLogger<InformationCacheService>.Instance),
			DmdataApiClientBuilder.UseOwnHttpClient(client),
			c => credential ?? new FakeOAuthCredential(c),
			_ => socketController,
			TimeProvider.System);
	}

	/// <summary>
	/// contract.list の応答
	/// </summary>
	public static string ContractList(params string[] classifications)
	{
		var items = classifications.Select((c, i) =>
			$$"""{"id":{{i + 1}},"planId":{{i + 1}},"planName":"テスト","classification":"{{c}}","price":{"day":0,"month":0},"start":"2026-01-01T00:00:00+09:00","isValid":true,"connectionCounts":1}""");
		return $$"""{"responseId":"test","responseTime":"2026-10-18T00:00:00+09:00","status":"ok","items":[{{string.Join(",", items)}}]}""";
	}
}

/// <summary>
/// 登録した応答だけを返し、それ以外は通信できなかったものとして扱う
/// </summary>
public sealed class StubHttpHandler : HttpMessageHandler
{
	private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _responses = [];

	public void Respond(string url, string body, HttpStatusCode status = HttpStatusCode.OK)
	{
		lock (_responses)
			_responses[url] = (status, body);
	}

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		var url = request.RequestUri?.GetLeftPart(UriPartial.Path) ?? "";
		lock (_responses)
		{
			if (!_responses.TryGetValue(url, out var response))
				throw new HttpRequestException("テストでは通信できません: " + request.RequestUri);
			return Task.FromResult(new HttpResponseMessage(response.Status) { Content = new StringContent(response.Body) });
		}
	}
}

/// <summary>
/// 認可サーバーへ接続せずにアクセストークンを返す認可情報
/// </summary>
public sealed class FakeOAuthCredential(HttpClient client) : OAuthCredential(client, [])
{
	/// <summary>
	/// アクセストークンの取得時に投げる例外
	/// </summary>
	public Exception? TokenError { get; set; }
	public int RevokeCount { get; private set; }

	protected override Task<(int, string)> GetAccessTokenAsync()
		=> TokenError is { } error ? Task.FromException<(int, string)>(error) : Task.FromResult((3600, "test-access-token"));

	public override Task RevokeAccessTokenAsync() => Task.CompletedTask;

	public override Task RevokeRefreshTokenAsync()
	{
		RevokeCount++;
		return Task.CompletedTask;
	}
}
