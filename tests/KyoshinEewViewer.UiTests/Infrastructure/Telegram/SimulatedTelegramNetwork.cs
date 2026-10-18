using System.Net;
using System.Net.Sockets;
using System.Web;
using DmdataSharp.Exceptions;
using DmdataSharp.Interfaces;

namespace KyoshinEewViewer.UiTests.Infrastructure.Telegram;

/// <summary>
/// dmdata と防災情報XMLの配信を模擬するテスト用の回線<br/>
/// 回線断・dmdataの障害をテストから切り替える。応答はメモリ内で生成し、実際の通信は行わない
/// </summary>
internal sealed class SimulatedTelegramNetwork : HttpMessageHandler
{
	public const string DmdataApiBaseUrl = "https://dmdata-api.fixture.invalid";
	public const string DmdataDataApiBaseUrl = "https://dmdata-data.fixture.invalid";
	public const string JmaFeedBaseUrl = "https://jma.fixture.invalid/developer/xml/feed/";
	private const string JmaDataBaseUrl = "https://jma.fixture.invalid/developer/xml/data/";

	private readonly Lock _gate = new();
	private readonly List<TelegramFixtures.EarthquakeTelegram> _earthquakes = [];
	private readonly List<string> _requests = [];

	/// <summary>
	/// 回線がつながっているか
	/// </summary>
	public bool Online { get; set; } = true;

	/// <summary>
	/// dmdata のサーバーが応答するか
	/// </summary>
	public bool DmdataAvailable { get; set; } = true;

	/// <summary>
	/// dmdata の WebSocket に接続できるか (APIは利用できる)
	/// </summary>
	public bool WebSocketAvailable { get; set; } = true;

	/// <summary>
	/// dmdata の契約情報を取得できるか (他のAPIは利用できる)
	/// </summary>
	public bool ContractListAvailable { get; set; } = true;

	/// <summary>
	/// 最後に作成された dmdata の WebSocket 接続
	/// </summary>
	public FakeDmdataSocketController? Socket { get; private set; }

	/// <summary>
	/// 両方の配信サービスで地震情報を配信する
	/// </summary>
	public void Publish(TelegramFixtures.EarthquakeTelegram telegram)
	{
		lock (_gate)
			_earthquakes.Add(telegram);
	}

	/// <summary>
	/// 受け付けた通信要求の数
	/// </summary>
	public int RequestCount(string urlPrefix)
	{
		lock (_gate)
			return _requests.Count(r => r.Contains(urlPrefix, StringComparison.Ordinal));
	}

	public IRedundantDmdataSocketController CreateSocket(IDmdataV2ApiClient apiClient)
	{
		var socket = new FakeDmdataSocketController(this, apiClient);
		Socket = socket;
		return socket;
	}

	/// <summary>
	/// WebSocket に接続できない場合はその原因を返す
	/// </summary>
	public Exception? WebSocketError()
	{
		if (!Online)
			return OfflineError();
		if (!DmdataAvailable)
			return new DmdataException("サーバーエラーが発生しています。 StatusCode: ServiceUnavailable");
		if (!WebSocketAvailable)
			return new System.Net.WebSockets.WebSocketException("WebSocketに接続できません (テスト)");
		return null;
	}

	private static HttpRequestException OfflineError()
		=> new(HttpRequestError.ConnectionError, "回線が切断されています (テスト)", new SocketException((int)SocketError.NetworkUnreachable));

	protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		=> Task.FromResult(CreateResponse(request, cancellationToken));

	private HttpResponseMessage CreateResponse(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		var uri = request.RequestUri ?? throw new InvalidOperationException("HTTP要求先がありません。");
		var url = uri.GetLeftPart(UriPartial.Path);
		var isDmdata = url.StartsWith(DmdataApiBaseUrl, StringComparison.Ordinal) || url.StartsWith(DmdataDataApiBaseUrl, StringComparison.Ordinal);
		if (!isDmdata && !url.StartsWith(JmaFeedBaseUrl, StringComparison.Ordinal) && !url.StartsWith(JmaDataBaseUrl, StringComparison.Ordinal))
		{
			NetworkAudit.Reject($"{request.Method} {uri}");
			throw new HttpRequestException("未登録の通信要求を拒否しました。");
		}

		// 回線断や障害を模擬した応答もテスト用の応答として扱う
		NetworkAudit.ApproveFixture(uri);
		lock (_gate)
			_requests.Add($"{request.Method} {uri}");

		if (!Online)
			throw OfflineError();
		if (isDmdata && (!DmdataAvailable || (!ContractListAvailable && url == DmdataApiBaseUrl + "/v2/contract")))
			return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("<html>Service Unavailable</html>") };

		var body = Respond(request.Method, uri, url);
		if (body == null)
			return new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("""{"responseId":"fixture","responseTime":"2026-10-18T10:00:00+09:00","status":"error","error":{"message":"not found","code":404}}""") };
		return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(request.Method == HttpMethod.Head ? "" : body) };
	}

	private string? Respond(HttpMethod method, Uri uri, string url)
	{
		TelegramFixtures.EarthquakeTelegram[] earthquakes;
		lock (_gate)
			earthquakes = [.. _earthquakes];

		switch (url)
		{
			case DmdataApiBaseUrl + "/v2/contract":
				return TelegramFixtures.DmdataContractList("telegram.earthquake", "eew.forecast");
			case DmdataApiBaseUrl + "/v2/telegram":
			{
				var query = HttpUtility.ParseQueryString(uri.Query);
				var types = query["type"]?.Split(',');
				var items = earthquakes.Select((t, i) => (t, i)).Where(x => types == null || types.Contains(x.t.Type));
				// PULL型ではカーソル以降に配信された電文だけを返す
				var isPull = int.TryParse(query["cursorToken"], out var cursor);
				if (isPull)
					items = items.Where(x => x.i >= cursor);
				// 履歴の取得は実時間で次の取得まで待つため短くし、PULL型の間隔はテストの時刻で進める
				return TelegramFixtures.DmdataTelegramList(items.Select(x => x.t), DmdataDataApiBaseUrl, earthquakes.Length, isPull ? 2000 : 1);
			}
			case DmdataApiBaseUrl + "/v2/parameter/earthquake/station":
			case DmdataApiBaseUrl + "/v2/parameter/tsunami/station":
				return TelegramFixtures.DmdataStationList();
			case JmaFeedBaseUrl + "eqvol_l.xml":
			case JmaFeedBaseUrl + "eqvol.xml":
				return TelegramFixtures.JmaFeed(url, earthquakes.Select(t => (JmaDataBaseUrl + t.Id + ".xml", t.Title, t.OriginTime.AddMinutes(3))));
			case JmaFeedBaseUrl + "extra_l.xml":
			case JmaFeedBaseUrl + "extra.xml":
				return TelegramFixtures.JmaFeed(url, []);
		}

		if (url.StartsWith(DmdataDataApiBaseUrl + "/v1/", StringComparison.Ordinal))
			return earthquakes.FirstOrDefault(t => url.EndsWith("/" + t.Id, StringComparison.Ordinal))?.Xml;
		if (url.StartsWith(JmaDataBaseUrl, StringComparison.Ordinal))
			return earthquakes.FirstOrDefault(t => url == JmaDataBaseUrl + t.Id + ".xml")?.Xml;
		return null;
	}
}
