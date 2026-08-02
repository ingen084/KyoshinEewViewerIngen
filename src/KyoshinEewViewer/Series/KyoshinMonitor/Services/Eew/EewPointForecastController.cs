using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Services;
using KyoshinMonitorLib;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;

/// <summary>
/// 外部アプリから提供された地点予測を保持し、表示用の値を定期的に更新する
/// </summary>
public class EewPointForecastController : IDisposable
{
	/// <summary>
	/// 1つのEEWに対して保持できる地点予測の数
	/// </summary>
	public const int MaxForecastCountPerEew = 16;

	/// <summary>
	/// 展開表示中の細かいカウントダウン更新の間隔
	/// </summary>
	private static readonly TimeSpan FineUpdateInterval = TimeSpan.FromMilliseconds(100);

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private EewController EewController { get; }
	private TimerService TimerService { get; }
	/// <summary>
	/// カウントダウンの基準となる現在時刻
	/// </summary>
	private Func<DateTime> GetCurrentTime { get; }

	private Lock _lock = new();
	/// <summary>
	/// (地震ID, 提供元, 地点名) をキーにした地点予測
	/// </summary>
	private Dictionary<(string EventId, string SourceKey, string PointName), EewPointForecast> Cache { get; } = [];

	private DispatcherTimer? FineUpdateTimer { get; set; }

	public EewPointForecastController(
		ILogManager logManager,
		KyoshinEewViewerConfiguration config,
		EewController eewController,
		TimerService timerService,
		Func<DateTime>? getCurrentTime = null)
	{
		Logger = logManager.GetLogger<EewPointForecastController>();
		Config = config;
		EewController = eewController;
		TimerService = timerService;
		GetCurrentTime = getCurrentTime ?? (() => timerService.CurrentTime);

		// EEWの表示直前に地点予測を合成する
		EewController.MergeAdditionalInformation = MergeForecasts;
		EewController.Cleared += Clear;
		// Webhook レスポンスの注入先をイベント経由で解決できるようにする
		EewController.PointForecastController = this;

		// 期限切れのEEWに紐づく地点予測を削除し、表示用の値を更新する
		TimerService.TimerElapsed += _ =>
		{
			RemoveOrphanedForecasts();
			RequestUpdateDisplayValues();
		};

		// 展開表示の設定が変わった場合は即座に反映する
		Config.Eew.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName is nameof(KyoshinEewViewerConfiguration.EewConfig.ExpandPointForecast)
				or nameof(KyoshinEewViewerConfiguration.EewConfig.PointForecastExpandIntensity))
				RequestUpdateDisplayValues();
		};
	}

	/// <summary>
	/// 地点予測を登録する
	/// </summary>
	/// <returns>登録された件数</returns>
	public int Update(IReadOnlyList<EewPointForecast> forecasts)
	{
		if (forecasts.Count <= 0)
			return 0;

		var registered = 0;
		lock (_lock)
		{
			foreach (var forecast in forecasts)
			{
				var key = (forecast.EventId, forecast.SourceKey, forecast.PointName);
				if (Cache.TryGetValue(key, out var current))
				{
					// 報数が古いものは無視する 同一報数の場合は新しく受信したものを使用する
					if (current.SerialNo > forecast.SerialNo)
						continue;
					if (current.SerialNo == forecast.SerialNo && current.ReceiveTime > forecast.ReceiveTime)
						continue;
				}
				else if (Cache.Count(p => p.Key.EventId == forecast.EventId) >= MaxForecastCountPerEew)
				{
					Logger.LogWarning($"地点予測の上限({MaxForecastCountPerEew}件)を超えたため破棄しました: {forecast.EventId}");
					continue;
				}

				Cache[key] = forecast;
				registered++;
			}
		}

		if (registered <= 0)
			return 0;

		RequestUpdateDisplayValues();
		EewController.RefreshEewUpdated(GetCurrentTime());
		return registered;
	}

	/// <summary>
	/// Webhook のレスポンスを解釈して地点予測を取り込む
	/// </summary>
	/// <param name="eventId">トリガー元のEEWの地震ID</param>
	/// <param name="serialNo">トリガー元のEEWの報数</param>
	/// <param name="responseBody">レスポンス本文</param>
	/// <param name="defaultSourceKey">提供元識別子が指定されていない場合に使用する値</param>
	/// <param name="defaultDisplaySource">提供元名が指定されていない場合に使用する値</param>
	/// <returns>利用者に表示する取り込み結果</returns>
	public string InjectFromWebhookResponse(string eventId, int serialNo, string responseBody, string defaultSourceKey, string defaultDisplaySource)
	{
		if (!Config.Eew.EnableExternalPointForecast)
			return "地点予測の取り込みが無効になっています。";

		// 期限切れなどで対象のEEWが存在しなくなっている場合は取り込まない
		if (!EewController.TryGetEew(eventId, out var eew) || eew == null)
			return $"地点予測の取り込み: 対象のEEWが存在しません({eventId})";

		PointForecastResponse? response;
		try
		{
			response = JsonSerializer.Deserialize(responseBody, PointForecastSerializerContext.Default.PointForecastResponse);
		}
		catch (JsonException)
		{
			// 外部から与えられた内容はログに残さない
			LogInvalidResponse(defaultSourceKey, "JSONの解析に失敗しました");
			return "地点予測の取り込み: JSONの解析に失敗しました。";
		}
		if (response?.Points is not { } points)
		{
			LogInvalidResponse(defaultSourceKey, "points が存在しません");
			return "地点予測の取り込み: points が存在しません。";
		}

		var sourceKey = NormalizeText(response.Source) ?? defaultSourceKey;
		var displaySource = NormalizeText(response.DisplaySource) ?? defaultDisplaySource;

		// 空配列は取り消しの意味として扱う
		if (points.Length <= 0)
		{
			RemoveSource(eventId, sourceKey);
			return "地点予測の取り込み: 0件(取り消し)";
		}

		var receiveTime = GetCurrentTime();
		var forecasts = new List<EewPointForecast>();
		var skipped = 0;
		foreach (var point in points)
		{
			if (BuildForecast(eventId, serialNo, sourceKey, displaySource, point, receiveTime, eew.Hypocenter?.OccurrenceTime) is { } forecast)
			{
				forecasts.Add(forecast);
				continue;
			}
			skipped++;
		}

		var registered = Update(forecasts);
		if (skipped > 0)
			LogInvalidResponse(sourceKey, $"{skipped}件の地点予測を破棄しました");
		return $"地点予測の取り込み: {registered}件" + (skipped > 0 ? $" / 破棄 {skipped}件" : "");
	}

	private EewPointForecast? BuildForecast(string eventId, int serialNo, string sourceKey, string displaySource, PointForecastResponsePoint point, DateTime receiveTime, DateTime? occurrenceTime)
	{
		if (NormalizeText(point.Name) is not { } name)
			return null;
		// 計測震度として取り得ない値は受け付けない
		if (point.Intensity is not { } intensity || !double.IsFinite(intensity) || intensity is < -3 or > 8)
			return null;

		// 座標は日本国内の範囲のみ受け付ける
		Location? location = null;
		if (point.Lat is { } lat && point.Lon is { } lon &&
			lat is >= 20 and <= 50 && lon is >= 120 and <= 155)
			location = new Location((float)lat, (float)lon);

		return new EewPointForecast
		{
			EventId = eventId,
			SourceKey = sourceKey,
			DisplaySource = displaySource,
			SerialNo = serialNo,
			PointName = name,
			Location = location,
			RealtimeIntensity = intensity,
			ArrivalTime = ParseArrivalTime(point.ArrivalAt, receiveTime),
			OccurrenceTime = occurrenceTime,
			ReceiveTime = receiveTime,
		};
	}

	/// <summary>
	/// 到達時刻を解釈する 解釈できない場合は到達時刻なしとして扱う
	/// </summary>
	private static DateTime? ParseArrivalTime(string? arrivalAt, DateTime receiveTime)
	{
		if (string.IsNullOrWhiteSpace(arrivalAt))
			return null;
		// RoundtripKind を指定することで、オフセットの有無が Kind に反映される
		if (!DateTime.TryParse(arrivalAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
			return null;

		// オフセットが指定されていない場合は日本標準時として扱い、指定されている場合は日本標準時へ変換する
		var arrivalTime = parsed.Kind == DateTimeKind.Unspecified
			? parsed
			: new DateTimeOffset(parsed).ToOffset(TimeSpan.FromHours(9)).DateTime;

		// 大幅にずれている値は信用しない
		if (arrivalTime < receiveTime.AddMinutes(-1) || arrivalTime > receiveTime.AddMinutes(10))
			return null;
		return arrivalTime;
	}

	/// <summary>
	/// 前後の空白を除去し、表示できない文字列を弾く
	/// </summary>
	private static string? NormalizeText(string? value, int maxRuneCount = 32)
	{
		if (string.IsNullOrWhiteSpace(value))
			return null;
		var trimmed = value.Trim();
		if (trimmed.Any(char.IsControl))
			return null;

		// サロゲートペアを壊さないよう Rune 単位で切り詰める
		var runeCount = 0;
		foreach (var rune in trimmed.EnumerateRunes())
		{
			runeCount++;
			if (runeCount <= maxRuneCount)
				continue;
			return string.Concat(trimmed.EnumerateRunes().Take(maxRuneCount).Select(r => r.ToString()));
		}
		return trimmed;
	}

	private DateTime _lastInvalidResponseLoggedAt = DateTime.MinValue;
	/// <summary>
	/// 不正なレスポンスを記録する ログが埋まらないよう頻度を抑える
	/// リプレイ時刻は逆行しうるため、レート制限は実時間基準で行う
	/// </summary>
	private void LogInvalidResponse(string sourceKey, string reason)
	{
		var now = TimerService.CurrentTime;
		if (now - _lastInvalidResponseLoggedAt < TimeSpan.FromSeconds(10))
			return;
		_lastInvalidResponseLoggedAt = now;
		Logger.LogWarning($"地点予測の取り込みに失敗しました({sourceKey}): {reason}");
	}

	/// <summary>
	/// 指定した提供元の地点予測をすべて削除する
	/// </summary>
	public void RemoveSource(string eventId, string sourceKey)
	{
		var removed = false;
		lock (_lock)
		{
			foreach (var key in Cache.Keys.Where(k => k.EventId == eventId && k.SourceKey == sourceKey).ToArray())
			{
				Cache.Remove(key);
				removed = true;
			}
		}
		if (!removed)
			return;
		EewController.RefreshEewUpdated(GetCurrentTime());
	}

	public void Clear()
	{
		lock (_lock)
			Cache.Clear();
		StopFineUpdateTimer();
	}

	/// <summary>
	/// EEW に地点予測を合成する
	/// </summary>
	private Models.Eew MergeForecasts(Models.Eew eew)
	{
		EewPointForecast[] forecasts;
		lock (_lock)
		{
			// キャンセルされたEEWの地点予測は表示しない
			if (eew.IsCancelled)
				return eew;
			forecasts = Cache.Where(p => p.Key.EventId == eew.Id).Select(p => p.Value).ToArray();
		}
		if (forecasts.Length <= 0)
			return eew;

		return eew with
		{
			// 到達状態で並び順が変わらないよう、計測震度を第一キーにする
			PointForecasts = forecasts
				.OrderByDescending(f => f.RealtimeIntensity)
				.ThenBy(f => f.ArrivalTime ?? DateTime.MaxValue)
				.ThenBy(f => f.SourceKey, StringComparer.Ordinal)
				.ToArray(),
		};
	}

	/// <summary>
	/// 紐づく EEW が存在しなくなった地点予測を削除する
	/// </summary>
	private void RemoveOrphanedForecasts()
	{
		string[] eventIds;
		lock (_lock)
			eventIds = Cache.Keys.Select(k => k.EventId).Distinct().ToArray();

		// EewController のロックを取得するため、自身のロックの外で存在確認を行う
		var orphanedEventIds = eventIds.Where(id => !EewController.TryGetEew(id, out _)).ToArray();
		if (orphanedEventIds.Length <= 0)
			return;

		lock (_lock)
		{
			foreach (var eventId in orphanedEventIds)
			{
				foreach (var key in Cache.Keys.Where(k => k.EventId == eventId).ToArray())
					Cache.Remove(key);
				Logger.LogInfo($"EEWの終了に伴い地点予測を削除しました: {eventId}");
			}
		}
	}

	/// <summary>
	/// 表示用の値を更新したあとに発火する 地図の再描画に使用する
	/// </summary>
	public event Action? DisplayValuesUpdated;

	/// <summary>
	/// 表示用の値の更新を UI スレッドへ依頼する
	/// </summary>
	private void RequestUpdateDisplayValues()
		=> Dispatcher.UIThread.Post(UpdateDisplayValues);

	/// <summary>
	/// 表示用の値を更新する UI スレッドから呼び出すこと
	/// </summary>
	private void UpdateDisplayValues()
	{
		EewPointForecast[] forecasts;
		lock (_lock)
			forecasts = Cache.Values.ToArray();

		var currentTime = GetCurrentTime();
		// 同一地点で複数の提供元がある場合、計測震度が最大のものだけを展開する
		var expandTargets = Config.Eew.ExpandPointForecast
			? forecasts
				.Where(f => f.Intensity >= Config.Eew.PointForecastExpandIntensity)
				.GroupBy(f => f.PointName, StringComparer.Ordinal)
				.Select(g => g
					.OrderByDescending(f => f.RealtimeIntensity)
					.ThenBy(f => f.ArrivalTime ?? DateTime.MaxValue)
					.ThenBy(f => f.SourceKey, StringComparer.Ordinal)
					.First())
				.ToHashSet()
			: [];

		foreach (var forecast in forecasts)
			forecast.UpdateDisplayValues(currentTime, expandTargets.Contains(forecast));

		// 展開表示中は細かい間隔で更新する
		if (forecasts.Any(f => f.IsExpanded))
			StartFineUpdateTimer();
		else
			StopFineUpdateTimer();

		// 毎フレーム描画に頼らず、更新をまとめてから地図の再描画を依頼する
		if (forecasts.Length > 0)
			DisplayValuesUpdated?.Invoke();
	}

	private void StartFineUpdateTimer()
	{
		if (FineUpdateTimer != null)
			return;
		FineUpdateTimer = new DispatcherTimer(FineUpdateInterval, DispatcherPriority.Normal, (_, _) => UpdateDisplayValues());
		FineUpdateTimer.Start();
	}

	private void StopFineUpdateTimer()
	{
		if (FineUpdateTimer == null)
			return;
		FineUpdateTimer.Stop();
		FineUpdateTimer = null;
	}

	public void Dispose()
	{
		StopFineUpdateTimer();
		EewController.Cleared -= Clear;
		if (EewController.MergeAdditionalInformation == MergeForecasts)
			EewController.MergeAdditionalInformation = null;
		if (EewController.PointForecastController == this)
			EewController.PointForecastController = null;
		GC.SuppressFinalize(this);
	}
}
