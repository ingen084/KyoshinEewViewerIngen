using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi.ApiModels;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi;

/// <summary>
/// P2P地震情報 API の HTTP クライアント
/// </summary>
public class P2pQuakeApiClient
{
	private const string BaseUrl = "https://api.p2pquake.net/v2";

	private HttpClient HttpClient { get; } = new HttpClient();

	public P2pQuakeApiClient()
	{
		HttpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"KEVi_{Utils.Version};twitter@ingen084");
	}

	/// <summary>
	/// 履歴情報を取得する
	/// </summary>
	/// <param name="codes">取得したい情報コード</param>
	/// <param name="limit">取得件数</param>
	/// <param name="offset">読み飛ばす件数</param>
	/// <returns>JSON文字列の配列</returns>
	public async Task<string> GetHistoryAsync(int[]? codes = null, int limit = 10, int offset = 0)
	{
		var url = $"{BaseUrl}/history?limit={limit}&offset={offset}";
		if (codes is { Length: > 0 })
		{
			foreach (var code in codes)
				url += $"&codes={code}";
		}

		using var response = await HttpClient.GetAsync(url);
		if (!response.IsSuccessStatusCode)
			throw new Exception($"P2P地震情報 APIの履歴取得に失敗しました (HTTP {(int)response.StatusCode})");

		return await response.Content.ReadAsStringAsync();
	}
}
