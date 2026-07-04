using DmdataSharp.ApiResponses.V2;
using DmdataSharp.Exceptions;
using DmdataSharp.Interfaces;
using DmdataSharp.WebSocketMessages.V2;
using KyoshinEewViewer.Core;
using Splat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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

	// 電文リストAPIのtypeパラメータに一度に指定できるデータ種類コードの上限
	private const int MaxTypesPerRequest = 5;

	// 電文リストの1回あたりの取得件数
	private const int TelegramListLimit = 50;

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
				"IXAC41", // 推計震度分布図(BUFR)
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

	public DmdataDataProcessor(ILogManager logManager, InformationCacheService cacheService)
	{
		Logger = logManager.GetLogger<DmdataDataProcessor>();
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
		Logger.LogDebug($"{e.Head.Type}{sb}");
#endif

		if (!TypeMap.Any(c => c.Value.Contains(e.Head.Type)))
			return null;
		var category = TypeMap.First(c => c.Value.Contains(e.Head.Type)).Key;

		if (category == InformationCategory.EewForecast || category == InformationCategory.EewWarning)
		{
			return (category, e);
		}

		// IXAC41(推計震度分布図)はBUFRバイナリ電文であり、XmlReportを持たないためXML電文特有の検証をスキップする
		var isBinaryTelegram = e.Head.Type == "IXAC41";
		if (!isBinaryTelegram)
		{
			if (e.XmlReport is null)
			{
				Logger.LogError($"WebSocket電文 {e.Id} の XMLReport がありません");
				return null;
			}
			if (e.XmlReport.Head.Title is null)
			{
				Logger.LogError($"WebSocket電文 {e.Id} の Title が取得できません");
				return null;
			}
		}

		await using var stream = e.GetBodyStream();
		var mstream = new MemoryStream();
		await stream.CopyToAsync(mstream);

		var telegram = new
		{
			Id = e.Id,
			Title = isBinaryTelegram ? "推計震度分布図" : e.XmlReport!.Control.Title,
			Type = e.Head.Type,
			DateTime = isBinaryTelegram ? e.Head.Time : e.XmlReport!.Control.DateTime,
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

		Logger.LogDebug($"get telegram list CursorToken: {CursorToken}");

		// typeパラメータはカンマ区切り(完全一致)で最大5つまでしか指定できないため、超える場合は複数回に分けて取得する
		string?[] typeQueries = [null];
		if (filterCategory is { } ca)
		{
			if (ca == InformationCategory.Typhoon)
				typeQueries = ["VPTW"];
			else
				typeQueries = TypeMap[ca].Chunk(MaxTypesPerRequest).Select(types => string.Join(",", types)).ToArray();
		}

		var items = new List<TelegramListResponse.Item>();
		var nextPoolingInterval = 0;
		string? nextPooling = null;
		foreach (var typeQuery in typeQueries)
		{
			// サーバー負荷軽減のため、分割したリクエストの間隔を空ける
			if (items.Count > 0)
				await Task.Delay(200);

			var resp = await ApiClient.GetTelegramListAsync(
				type: typeQuery,
				xmlReport: true,
				test: receiveTraining ? "including" : "no",
				cursorToken: useCursorToken ? CursorToken : null,
				limit: TelegramListLimit
			);

			if (resp.Status != "ok")
				throw new DmdataException($"dmdataからのリストの取得に失敗しました status: {resp.Status}, errorMessage: {resp.Error?.Message}");

			items.AddRange(resp.Items);
			nextPoolingInterval = Math.Max(nextPoolingInterval, resp.NextPoolingInterval);
			nextPooling = resp.NextPooling;
		}

		// 分割して取得した場合、全タイプを一度に指定した場合と同じ結果になるよう受信時刻の新しい順に上限件数のみ残す
		// (発行頻度の低いタイプだけが過剰に過去まで遡ることを防ぐ)
		IEnumerable<TelegramListResponse.Item> mergedItems = items;
		if (typeQueries.Length > 1)
			mergedItems = items.OrderByDescending(i => i.ReceivedTime).Take(TelegramListLimit);

		Logger.LogDebug($"dmdata items count: {items.Count}");
		foreach (var item in mergedItems)
		{
			// IXAC41(推計震度分布図)はBUFRバイナリ電文であり、Formatが"xml"以外・XmlReportがnullになるため個別に許可する
			var isBinaryTelegram = item.Head.Type == "IXAC41";
			if ((item.Format != "xml" && !isBinaryTelegram) || ReceivedTelegrams.Contains(item.Id))
				continue;

			result.Add((
				item.Id,
				isBinaryTelegram ? "推計震度分布図" : item.XmlReport!.Control.Title!,
				item.Head.Type,
				isBinaryTelegram ? item.Head.Time : item.XmlReport!.Control.DateTime));

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
			CursorToken = nextPooling;
			ReceivedTelegrams.Clear();
		}

		Logger.LogDebug($"get telegram list nextpooling: {nextPoolingInterval}");
		if (result.Count != 0)
			result.Reverse();
		return (result.ToArray(), nextPoolingInterval);
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
				Logger.LogInfo($"dmdataから取得しています: {key}");
				return await (ApiClient?.GetTelegramStreamAsync(key) ?? throw new Exception("ApiClientが初期化されていません"));
			}
			catch (DmdataRateLimitExceededException ex)
			{
				Logger.LogWarning($"レートリミットに引っかかっています try{count} ({ex.RetryAfter})");
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
