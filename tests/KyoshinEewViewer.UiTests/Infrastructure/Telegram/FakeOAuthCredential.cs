using DmdataSharp.Authentication.OAuth;

namespace KyoshinEewViewer.UiTests.Infrastructure.Telegram;

/// <summary>
/// 認可サーバーへ接続せずにアクセストークンを返す認可情報
/// </summary>
internal sealed class FakeOAuthCredential(HttpClient client) : OAuthCredential(client, [])
{
	protected override Task<(int, string)> GetAccessTokenAsync() => Task.FromResult((3600, "fixture-access-token"));

	public override Task RevokeAccessTokenAsync() => Task.CompletedTask;

	public override Task RevokeRefreshTokenAsync() => Task.CompletedTask;
}
