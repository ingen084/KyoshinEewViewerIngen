using DmdataSharp.ApiResponses.V2.Parameters;
using DmdataSharp.Exceptions;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.Earthquake.Converters;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;
using KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi;
using KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi.ApiModels;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinMonitorLib;
using ReactiveUI;
using Sentry;
using Splat;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Earthquake.Services;

/// <summary>
/// 地震情報の更新を担う
/// </summary>
public class EarthquakeWatchService : ReactiveObject
{
	private readonly string[] _targetTitles = ["震度速報", "震源に関する情報", "震源・震度に関する情報", "顕著な地震の震源要素更新のお知らせ", "長周期地震動に関する観測情報"];

	public EarthquakeStationParameterResponse? Stations { get; private set; }
	public ObservableCollection<EarthquakeEvent> Earthquakes { get; } = [];
	public event Action<EarthquakeEvent, bool, bool, EarthquakeInformationFragment?, JmaIntensity?>? EarthquakeUpdated;

	public event Action? Failed;
	public event Action? SourceSwitching;
	public event Action<string>? SourceSwitched;

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }

	/// <summary>
	/// 信頼できないEventIdソース由来の現在のイベント（最大1件保持）
	/// </summary>
	private EarthquakeEvent? _currentUnreliableEvent;

	public EarthquakeWatchService(
		ILogManager logManager,
		KyoshinEewViewerConfiguration config,
		TelegramProvideService telegramProvider,
		DmdataRedundantTelegramPublisher dmdata,
		AxisInformationProvider axisProvider,
		P2pQuakeApiInformationProvider p2pProvider)
	{
		SplatRegistrations.RegisterLazySingleton<EarthquakeWatchService>();

		Logger = logManager.GetLogger<EarthquakeWatchService>();
		Config = config;

		axisProvider.Initialize();
		p2pProvider.Initialize();
		axisProvider.MessageReceived += OnAxisMessageReceived;
		p2pProvider.MessageReceived += OnP2pMessageReceived;

		telegramProvider.Subscribe(
			InformationCategory.Earthquake,
			async (s, t) =>
			{
				SourceSwitching?.Invoke();

				if (s.Contains("DM-D.S.S") && Stations == null)
					try
					{
						Stations = await dmdata.GetEarthquakeStationsAsync();
					}
					catch (DmdataForbiddenException) { }
					catch (Exception ex)
					{
						Logger.LogError(ex, "地震観測点情報取得中に問題が発生しました");
					}

				Earthquakes.Clear();
				_currentUnreliableEvent = null;
				foreach (var h in t.OrderBy(h => h.ArrivalTime).ToArray())
				{
					try
					{
						await ProcessInformation(h, hideNotice: true);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex, "キャッシュ破損疑いのため削除します");
						try
						{
							// キャッシュ破損時用
							h.Cleanup();
							await ProcessInformation(h, hideNotice: true);
						}
						catch (Exception ex2)
						{
							// その他のエラー発生時は処理を中断させる
							Logger.LogError(ex2, "初回電文取得中に問題が発生しました");
						}
						return;
					}
				}
				// 電文データがない(震源情報しかないなどの)データを削除する
				foreach (var eq in Earthquakes.Where(e => e.Fragments.All(f => f is not IntensityInformationFragment and not HypocenterAndIntensityInformationFragment)).ToArray())
					Earthquakes.Remove(eq);

				foreach (var eq in Earthquakes)
					EarthquakeUpdated?.Invoke(eq, true, false, null, null);
				SourceSwitched?.Invoke(s);
			},
			async t =>
			{
				var trans = SentrySdk.StartTransaction("earthquake", "arrived");
				try
				{
					await ProcessInformation(t);
					trans.Finish();
				}
				catch (Exception ex)
				{
					trans.Finish(ex);
				}
			},
			s =>
			{
				if (s.isAllFailed)
					Failed?.Invoke();
				else
					SourceSwitching?.Invoke();
			});

		telegramProvider.Subscribe(
			InformationCategory.Tsunami,
			(_, _) =>
			{
				// あくまで震源情報の代わりなので津波情報はとりあえずなにもしない
				// 問題が発生したらなんとかする
				return Task.CompletedTask;
			},
			async t =>
			{
				try
				{
					await ProcessTsunamiInformation(t);
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, "津波情報による震源情報の更新に失敗しました。");
				}
			},
			_ => { }
		);
	}

	public async Task ProcessTsunamiInformation(Telegram telegram, bool hideNotice = false)
	{
		await using var stream = await telegram.GetBodyAsync();
		using var report = new JmaXmlDocument(stream);
		if (report.Control.Title != "津波警報・注意報・予報a")
			return;

		var fragments = EarthquakeInformationFragment.CreateFromTsunamiJmxXmlDocument(telegram, report);
		foreach (var (eventId, fragment) in fragments)
		{
			// TODO 作成できるようにしておいた方がよさそう
			var eq = Earthquakes.FirstOrDefault(e => e.EventId == eventId);
			if (eq == null)
			{
				Logger.LogWarning($"イベントID {eventId} が見つからなかったため津波情報による震源情報の更新を行いませんでした。");
				continue;
			}
			eq.AddFragment(fragment);
			if (!hideNotice)
				EarthquakeUpdated?.Invoke(eq, false, false, null, null);
		}
	}
	public async Task<EarthquakeEvent?> ProcessInformation(Telegram telegram, bool dryRun = false, bool hideNotice = false)
	{
		await using var stream = await telegram.GetBodyAsync();
		using var report = new JmaXmlDocument(stream);

		try
		{
			// サポート外であれば見なかったことにする
			if (!_targetTitles.Contains(report.Control.Title))
				return null;

			var data = JmaXmlEarthquakeConverter.Convert(report);
			if (data == null)
				return null;

			// JMA XML用の遅延パースプロバイダを生成
			var isOnlyAreas = report.Control.Title == "震度速報";
			IEarthquakeDisplayDataProvider? provider = data.Intensity != null
				? new JmaXmlDisplayDataProvider(telegram, isOnlyAreas)
				: null;

			return ProcessInformationFromData(data, provider, dryRun, hideNotice, telegram.Key);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "デシリアライズ時に例外が発生しました");
			return null;
		}
	}

	/// <summary>
	/// 中間表現データから地震情報を処理する
	/// </summary>
	public EarthquakeEvent? ProcessInformationFromData(
		EarthquakeInformationData data,
		IEarthquakeDisplayDataProvider? displayDataProvider = null,
		bool dryRun = false,
		bool hideNotice = false,
		string? telegramKey = null)
	{
		try
		{
			// サポート外であれば見なかったことにする
			if (!_targetTitles.Contains(data.Title))
				return null;

			var isCreated = false;
			var eq = Earthquakes.FirstOrDefault(e => e.EventId == data.EventId);
			if (eq == null || dryRun)
			{
				eq = new EarthquakeEvent(data.EventId);
				if (!dryRun)
					Earthquakes.Insert(0, eq);
				isCreated = true;
			}

			var prevInt = eq.Intensity;

			var fragment = eq.ProcessIntermediateData(data, displayDataProvider, telegramKey);

			// 信頼できないソースの打ち消し判定
			TryDismissUnreliableEvent(data, eq);

			if (!hideNotice)
				EarthquakeUpdated?.Invoke(eq, false, dryRun, fragment, isCreated ? null : prevInt);
			return eq;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "中間表現データの処理中に例外が発生しました");
			return null;
		}
	}

	/// <summary>
	/// 信頼できないEventIdソースのイベントを打ち消す
	/// </summary>
	private void TryDismissUnreliableEvent(EarthquakeInformationData data, EarthquakeEvent eq)
	{
		if (_currentUnreliableEvent == null)
			return;
		// 保持モード時は打ち消さない
		if (Config.P2pQuakeApi.KeepEarthquakeEvents)
			return;
		// 信頼できないソース自身は対象外
		if (eq.IsUnreliableEventIdSource)
			return;

		// 震度速報か否かの一致を確認
		var unreliableIsSokuhou = _currentUnreliableEvent.IsSokuhou && !_currentUnreliableEvent.IsHypocenterOnly;
		var receivedIsSokuhou = data.Title == "震度速報";
		if (unreliableIsSokuhou != receivedIsSokuhou)
			return;

		// 発生/検知時刻の一致
		if (_currentUnreliableEvent.Time != eq.Time)
			return;

		// マグニチュードの一致
		if (_currentUnreliableEvent.Magnitude != eq.Magnitude)
			return;

		// 震央地名/代表地域名の一致
		if (_currentUnreliableEvent.Place != eq.Place)
			return;

		Logger.LogInfo($"信頼できるソースで同一地震情報を受信したため、参考情報を削除します: {_currentUnreliableEvent.EventId}");
		Earthquakes.Remove(_currentUnreliableEvent);
		_currentUnreliableEvent = null;
	}

	/// <summary>
	/// P2P地震情報のメッセージを処理する
	/// </summary>
	private void OnP2pMessageReceived(P2pQuakeApiBaseMessage message)
	{
		if (message is not P2pQuakeApiEarthquakeMessage earthquakeMessage)
			return;

		try
		{
			var data = P2pQuakeApiEarthquakeConverter.Convert(earthquakeMessage);
			if (data == null)
				return;

			// DisplayDataProviderの生成
			IEarthquakeDisplayDataProvider? provider = null;
			if (earthquakeMessage.Points is { Length: > 0 } points)
			{
				var maxIntensity = data.Intensity?.MaxIntensity ?? JmaIntensity.Unknown;
				provider = new P2pQuakeApiDisplayDataProvider(points, maxIntensity);
			}

			var keepEvents = Config.P2pQuakeApi.KeepEarthquakeEvents;

			// 既存の信頼できないイベントがある場合、同一地震の続報かどうかを判定
			if (_currentUnreliableEvent != null)
			{
				if (IsSameP2pEarthquake(_currentUnreliableEvent, data))
				{
					// 同一地震の続報: 既存イベントにフラグメントを追加して更新
					var fragment = _currentUnreliableEvent.ProcessIntermediateData(data, provider);
					if (fragment != null)
						EarthquakeUpdated?.Invoke(_currentUnreliableEvent, false, false, fragment, null);
					return;
				}

				// 異なる地震: 保持モードでない場合は既存の信頼できないイベントを削除
				if (!keepEvents)
					Earthquakes.Remove(_currentUnreliableEvent);
				_currentUnreliableEvent = null;
			}

			var eq = ProcessInformationFromData(data, provider);
			if (eq != null)
			{
				eq.IsUnreliableEventIdSource = true;
				eq.Subtitle = "P2P地震情報";
				_currentUnreliableEvent = eq;
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "P2P地震情報の処理中にエラーが発生しました");
		}
	}

	/// <summary>
	/// P2P地震情報の既存イベントと新しいデータが同一地震かどうかを判定する
	/// 震度速報は検知時刻、震源情報は発生時刻のため時刻がずれることがあるため、余裕を持って判定する
	/// </summary>
	private static bool IsSameP2pEarthquake(EarthquakeEvent existing, EarthquakeInformationData newData)
	{
		var newTime = newData.Hypocenter?.OccurrenceTime ?? newData.Intensity?.DetectionTime;
		if (newTime == null)
			return false;

		return Math.Abs((existing.Time - newTime.Value).TotalMinutes) < 5;
	}

	/// <summary>
	/// AXIS地震情報のメッセージを処理する
	/// </summary>
	private void OnAxisMessageReceived(AxisWebSocketMessage message)
	{
		if (message.Channel != "jmx-seismology")
			return;

		try
		{
			var earthquakeMessage = message.Message.Deserialize<EarthquakeMessage>();
			if (earthquakeMessage == null)
				return;

			var data = AxisEarthquakeConverter.Convert(earthquakeMessage);
			if (data == null)
				return;

			// DisplayDataProviderの生成
			IEarthquakeDisplayDataProvider? provider = null;
			if (data.Intensity != null && earthquakeMessage.Body.Intensity?.Observation is { } observation)
			{
				var isOnlyAreas = earthquakeMessage.Control.Title == "震度速報";
				provider = new AxisDisplayDataProvider(observation, isOnlyAreas);
			}

			ProcessInformationFromData(data, provider);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "AXIS地震情報の処理中にエラーが発生しました");
		}
	}
}
