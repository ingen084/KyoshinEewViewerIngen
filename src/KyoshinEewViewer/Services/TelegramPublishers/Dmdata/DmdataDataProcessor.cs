using DmdataSharp.Exceptions;
using DmdataSharp.Interfaces;
using DmdataSharp.WebSocketMessages.V2;
using KyoshinEewViewer.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Services.TelegramPublishers.Dmdata;

/// <summary>
/// Dmdataからのデータ取得と処理を担当する
/// </summary>
public class DmdataDataProcessor
{
	private ILogger Logger { get; }
	private InformationCacheService CacheService { get; }
	private IDmdataV2ApiClient? ApiClient { get; set; }

	// 受信済み電文リストの上限
	private const int MaxReceivedTelegramsCount = 1000;

	// カテゴリからタイプ郡へのマップ
	private static readonly Dictionary<InformationCategory, string[]> TypeMap = new()
	{
		{
			InformationCategory.Earthquake,
			[
				"VXSE51",
				"VXSE52",
				"VXSE53",
				"VXSE61",
				"VXSE62",
			]
		},
		{
			InformationCategory.EewForecast,
			[
				"VXSE42",
				"VXSE45",
			]
		},
		{ InformationCategory.EewWarning, [ "VXSE43" ] },
		{
			InformationCategory.Tsunami,
			[
				"VTSE41",
				"VTSE51",
				"VTSE52",
			]
		},
		{
			InformationCategory.Typhoon,
			[
				"VPTW60",
				"VPTW61",
				"VPTW62",
				"VPTW63",
				"VPTW64",
				"VPTW65",
			]
		}
	};

	private string? CursorToken { get; set; }
	private List<string> ReceivedTelegrams { get; } = [];

	public DmdataDataProcessor(ILogger<DmdataDataProcessor> logger, InformationCacheService cacheService)
	{
		Logger = logger;
		CacheService = cacheService;
	}

	/// <summary>
	/// APIクライアントを設定する
	/// </summary>
	public void SetApiClient(IDmdataV2ApiClient? apiClient)
	{
		ApiClient = apiClient;
	}

	/// <summary>
	/// カーソルトークンと受信済み電文リストをリセットする
	/// </summary>
	public void ResetState()
	{
		CursorToken = null;
		ReceivedTelegrams.Clear();
	}

	/// <summary>
	/// WebSocketから受信したデータを処理する
	/// </summary>
	/// <returns>処理された電文情報(カテゴリ、電文オブジェクト)</returns>
	public async Task<(InformationCategory category, object telegram)?> ProcessWebSocketDataAsync(DataWebSocketMessage? e)
	{
		if (e is null)
		{
			Logger.LogError("WebSocketデータがnullです");
			return null;
		}

#if DEBUG
		var sb = new System.Text.StringBuilder();
		foreach (var p in e.Passing)
			sb.Append($" {p.Name}:{p.Time:ss.fff}");
		Logger.LogDebug("{Type}{Sb}", e.Head.Type, sb);
#endif

		if (e.XmlReport is null)
		{
			Logger.LogError("WebSocket電文 {Id} の XMLReport がありません", e.Id);
			return null;
		}
		if (e.XmlReport.Head.Title is null)
		{
			Logger.LogError("WebSocket電文 {Id} の Title が取得できません", e.Id);
			return null;
		}

		if (!TypeMap.Any(c => c.Value.Contains(e.Head.Type)))
			return null;
		var category = TypeMap.First(c => c.Value.Contains(e.Head.Type)).Key;

		if (category == InformationCategory.EewForecast || category == InformationCategory.EewWarning)
		{
			return (category, e);
		}

		await using var stream = e.GetBodyStream();
		var mstream = new MemoryStream();
		await stream.CopyToAsync(mstream);

		var telegram = new
		{
			Id = e.Id,
			Title = e.XmlReport.Control.Title,
			Type = e.Head.Type,
			DateTime = e.XmlReport.Control.DateTime,
			Body = mstream.ToArray()
		};

		_ = Task.Run(async () =>
		{
			try
			{
				mstream.Seek(0, SeekOrigin.Begin);
				await CacheService.CacheTelegramAsync(e.Id, () => mstream);
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "電文のキャッシュに失敗しました");
			}
			finally
			{
				mstream.Dispose();
			}
		}).ConfigureAwait(false);

		return (category, telegram);
	}

	/// <summary>
	/// 電文リストを取得する
	/// </summary>
	/// <param name="filterCategory">フィルタするカテゴリ(nullの場合はすべて)</param>
	/// <param name="useCursorToken">カーソルトークンを使用するか</param>
	/// <param name="receiveTraining">訓練報を受信するか</param>
	/// <returns>電文情報の配列と次回のポーリング間隔</returns>
	public async Task<((string key, string title, string type, DateTime arrivalTime)[], int nextPoolingInterval)> FetchListAsync(
		InformationCategory? filterCategory,
		bool useCursorToken,
		bool receiveTraining)
	{
		if (ApiClient == null)
			throw new DmdataException("ApiClientが初期化されていません");

		var result = new List<(string key, string title, string type, DateTime arrivalTime)>();

		Logger.LogDebug("get telegram list CursorToken: {CursorToken}", CursorToken);

		string? type = null;
		if (filterCategory is { } ca)
		{
			if (ca == InformationCategory.Typhoon)
				type = "VPTW";
			else
				type = string.Join(",", TypeMap[ca]);
		}
		var resp = await ApiClient.GetTelegramListAsync(
			type: type,
			xmlReport: true,
			test: receiveTraining ? "including" : "no",
			cursorToken: useCursorToken ? CursorToken : null,
			limit: 50
		);

		if (resp.Status != "ok")
			throw new DmdataException($"dmdataからのリストの取得に失敗しました status: {resp.Status}, errorMessage: {resp.Error?.Message}");

		Logger.LogDebug("dmdata items count: {Length}", resp.Items.Length);
		foreach (var item in resp.Items)
		{
			if (item.Format != "xml" || ReceivedTelegrams.Contains(item.Id))
				continue;

			result.Add((
				item.Id,
				item.XmlReport!.Control.Title!,
				item.Head.Type,
				item.XmlReport!.Control.DateTime));

			if (!useCursorToken)
			{
				ReceivedTelegrams.Add(item.Id);
				// メモリリーク防止のため、上限を超えたら古いものから削除
				if (ReceivedTelegrams.Count > MaxReceivedTelegramsCount)
					ReceivedTelegrams.RemoveRange(0, MaxReceivedTelegramsCount / 2);
			}
		}
		if (useCursorToken)
		{
			CursorToken = resp.NextPooling;
			ReceivedTelegrams.Clear();
		}

		Logger.LogDebug("get telegram list nextpooling: {NextPoolingInterval}", resp.NextPoolingInterval);
		if (result.Count != 0)
			result.Reverse();
		return (result.ToArray(), resp.NextPoolingInterval);
	}

	/// <summary>
	/// 電文の内容を取得する
	/// </summary>
	public async Task<Stream> FetchContentAsync(string key)
	{
		var count = 0;
		while (true)
		{
			count++;
			try
			{
				Logger.LogInformation("dmdataから取得しています: {Key}", key);
				return await (ApiClient?.GetTelegramStreamAsync(key) ?? throw new Exception("ApiClientが初期化されていません"));
			}
			catch (DmdataRateLimitExceededException ex)
			{
				Logger.LogWarning("レートリミットに引っかかっています try{Count} ({RetryAfter})", count, ex.RetryAfter);
				if (count > 10)
					throw;
				await Task.Delay(200);
			}
		}
	}

	/// <summary>
	/// 指定されたタイプが購読対象かどうかを判定する
	/// </summary>
	public static bool IsSubscribedType(string type, IEnumerable<InformationCategory> subscribingCategories)
	{
		if (!TypeMap.Any(c => c.Value.Contains(type)))
			return false;

		var category = TypeMap.First(c => c.Value.Contains(type)).Key;
		return subscribingCategories.Contains(category);
	}

	/// <summary>
	/// タイプからカテゴリを取得する
	/// </summary>
	public static InformationCategory? GetCategoryFromType(string type)
	{
		if (!TypeMap.Any(c => c.Value.Contains(type)))
			return null;

		return TypeMap.First(c => c.Value.Contains(type)).Key;
	}

	/// <summary>
	/// カテゴリから電文タイプを取得する
	/// </summary>
	public static string[] GetTypesFromCategory(InformationCategory category)
	{
		return TypeMap.TryGetValue(category, out var types) ? types : [];
	}
}
