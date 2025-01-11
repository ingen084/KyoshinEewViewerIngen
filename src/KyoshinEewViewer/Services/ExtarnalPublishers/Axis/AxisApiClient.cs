using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.Axis;

public class AxisApiClient
{
	private HttpClient HttpClient { get; } = new HttpClient();

	public string? Jwt { get; set; }

	public AxisApiClient()
	{
		HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"KEVi_{Utils.Version};twitter@ingen084");
	}

	public async Task<string[]> GetServers()
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, "https://axis.prioris.jp/api/server/list/");
		request.Headers.Add("Authorization", "Bearer " + Jwt);

		using var response = await HttpClient.SendAsync(request);
		if (!response.IsSuccessStatusCode)
			throw new Exception("サーバー一覧の取得に失敗しました");

		var json = await response.Content.ReadAsStringAsync();
		var servers = JsonSerializer.Deserialize<GetServersResponse>(json);
		if (servers == null)
			throw new Exception("レスポンスのパースに失敗しました");

		return servers.Servers ?? throw new Exception("サーバー一覧が取得できません");
	}

	public async Task<string> RefreshJwt()
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, "https://axis.prioris.jp/api/token/refresh/");
		request.Headers.Add("Authorization", "Bearer " + Jwt);
		using var response = await HttpClient.SendAsync(request);
		if (!response.IsSuccessStatusCode)
			throw new Exception("JWT のリフレッシュに失敗しました");

		var json = await response.Content.ReadAsStringAsync();
		var refreshResponse = JsonSerializer.Deserialize<RefreshTokenResponse>(json);
		if (refreshResponse == null)
			throw new Exception("レスポンスのパースに失敗しました");

		return Jwt = refreshResponse.Token ?? throw new Exception("新しいトークンが取得できません");
	}
}
