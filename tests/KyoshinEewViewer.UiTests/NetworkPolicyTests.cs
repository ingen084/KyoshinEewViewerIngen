using System.Net;
using System.Net.Sockets;
using KyoshinEewViewer.UiTests.Infrastructure;

namespace KyoshinEewViewer.UiTests;

public sealed class NetworkPolicyTests
{
	[Fact]
	public async Task 登録済みHTTP要求はソケットを使わず保存済み応答を返す()
	{
		using var handler = new FixtureHttpHandler();
		// クエリ付きURLでも診断イベントと登録内容を照合できることを確認する。
		var uri = new Uri("https://fixture.invalid/reports/earthquake.json?type=VXSE53&limit=10");
		handler.Register(HttpMethod.Get, uri, "{\"title\":\"地震情報\"}");
		using var client = new HttpClient(handler);
		Assert.Equal("{\"title\":\"地震情報\"}", await client.GetStringAsync(uri, TestContext.Current.CancellationToken));
	}

	public static bool HttpProbeEnabled => Environment.GetEnvironmentVariable("KEVI_UI_TEST_NETWORK_PROBE") == "http";
	public static bool DnsProbeEnabled => Environment.GetEnvironmentVariable("KEVI_UI_TEST_NETWORK_PROBE") == "dns";
	public static bool SocketProbeEnabled => Environment.GetEnvironmentVariable("KEVI_UI_TEST_NETWORK_PROBE") == "socket";
	public static bool FixtureProbeEnabled => Environment.GetEnvironmentVariable("KEVI_UI_TEST_NETWORK_PROBE") == "fixture";

	[Fact(Skip = "通信禁止基盤の失敗検査用です。", SkipUnless = nameof(FixtureProbeEnabled))]
	public async Task FixtureRequestCaughtByAppStillFails()
	{
		using var client = new HttpClient(new FixtureHttpHandler());
		try { await client.GetAsync("https://fixture.invalid/unregistered", TestContext.Current.CancellationToken); }
		catch (HttpRequestException) { }
	}

	// 専用ランナーが各ケースを別プロセスで有効化し、失敗すること自体を検査する。
	[Fact(Skip = "通信禁止基盤の失敗検査用です。", SkipUnless = nameof(HttpProbeEnabled))]
	public async Task HttpRequestCaughtByAppStillFails()
	{
		try
		{
			using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
			await client.GetAsync("http://127.0.0.1:9/unregistered", TestContext.Current.CancellationToken);
		}
		catch (HttpRequestException) { }
	}

	[Fact(Skip = "通信禁止基盤の失敗検査用です。", SkipUnless = nameof(DnsProbeEnabled))]
	public async Task DnsRequestCaughtByAppStillFails()
	{
		try { await Dns.GetHostAddressesAsync("localhost", TestContext.Current.CancellationToken); }
		catch (SocketException) { }
	}

	[Fact(Skip = "通信禁止基盤の失敗検査用です。", SkipUnless = nameof(SocketProbeEnabled))]
	public void SocketRequestCaughtByAppStillFails()
	{
		try
		{
			using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(IPAddress.Loopback, 9);
		}
		catch (SocketException) { }
	}
}
