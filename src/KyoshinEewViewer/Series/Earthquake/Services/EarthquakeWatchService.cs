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
	public event Action<EarthquakeUpdateEventArgs>? EarthquakeUpdated;

	public event Action? Failed;
	public event Action? SourceSwitching;
	public event Action<string>? SourceSwitched;

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }

	private readonly UnreliableEventManager _unreliableEventManager;
	private readonly IObservationDiffCalculator _diffCalculator;

	public EarthquakeWatchService(
		ILogManager logManager,
		KyoshinEewViewerConfiguration config,
		TelegramProvideService telegramProvider,
		DmdataRedundantTelegramPublisher dmdata,
		AxisInformationProvider axisProvider,
		P2pQuakeApiInformationProvider p2pProvider,
		IObservationDiffCalculator diffCalculator)
	{
		SplatRegistrations.RegisterLazySingleton<EarthquakeWatchService>();
		SplatRegistrations.RegisterLazySingleton<IObservationDiffCalculator, ObservationDiffCalculator>();

		Logger = logManager.GetLogger<EarthquakeWatchService>();
		Config = config;
		_unreliableEventManager = new UnreliableEventManager(Earthquakes, config);
		_diffCalculator = diffCalculator;

		axisProvider.Initialize();
		axisProvider.MessageReceived += OnAxisMessageReceived;
		#if DEBUG
		p2pProvider.Initialize();
		p2pProvider.MessageReceived += OnP2pMessageReceived;
		#endif

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
				_unreliableEventManager.Reset();
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
					EarthquakeUpdated?.Invoke(new(eq, true, false, null, null, null));
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
				EarthquakeUpdated?.Invoke(new(eq, false, false, null, null, null));
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

			return ProcessInformationFromData(data, dryRun, hideNotice, telegram.Key);
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

			var result = eq.ProcessIntermediateData(data, telegramKey);
			var regionDiff = ComputeRegionDiff(result, eq);

			// 信頼できないソースの打ち消し判定
			_unreliableEventManager.TryDismiss(data, eq);

			if (!hideNotice)
				EarthquakeUpdated?.Invoke(new(eq, false, dryRun, result.Fragment, result.PreviousState, regionDiff));
			return eq;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "中間表現データの処理中に例外が発生しました");
			return null;
		}
	}

	/// <summary>
	/// P2P地震情報 JSON APIのメッセージを処理する
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

			// 既存の信頼できないイベントがある場合、同一地震の続報かどうかを判定
			if (_unreliableEventManager.CurrentEvent != null)
			{
				if (_unreliableEventManager.IsSameEarthquake(data))
				{
					// 同一地震の続報: 既存イベントにフラグメントを追加して更新
					var result = _unreliableEventManager.CurrentEvent.ProcessIntermediateData(data);
					if (result.Fragment != null)
					{
						var regionDiff = ComputeRegionDiff(result, _unreliableEventManager.CurrentEvent);
						EarthquakeUpdated?.Invoke(new(_unreliableEventManager.CurrentEvent, false, false, result.Fragment, result.PreviousState, regionDiff));
					}
					return;
				}

				// 異なる地震: 保持モードでない場合は既存の信頼できないイベントを削除
				_unreliableEventManager.RemoveCurrentIfNotKeeping();
			}

			// 信頼できるソースで同等の地震情報が既に存在する場合はスキップ
			if (_unreliableEventManager.HasMatchingReliableEvent(data))
			{
				Logger.LogDebug($"信頼できるソースで同等の地震情報が存在するため、P2P地震情報をスキップします: {data.Title}");
				return;
			}

			var eq = ProcessInformationFromData(data);
			if (eq != null)
			{
				eq.IsUnreliableEventIdSource = true;
				eq.Subtitle = "P2P地震情報";
				_unreliableEventManager.SetCurrentEvent(eq);
			}
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "P2P地震情報 JSON APIの処理中にエラーが発生しました");
		}
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

			ProcessInformationFromData(data);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "AXIS地震情報の処理中にエラーが発生しました");
		}
	}

	/// <summary>
	/// 処理結果から観測地域の差分を計算する
	/// </summary>
	private ObservationDiff? ComputeRegionDiff(EarthquakeProcessResult result, EarthquakeEvent eq)
	{
		if (result.PreviousState == null || result.Fragment == null)
			return null;

		return result.PreviousState.ObservationPrefs != null || eq.LatestObservationPrefs != null
			? _diffCalculator.ComputeFromPrefs(result.PreviousState.ObservationPrefs, eq.LatestObservationPrefs)
			: _diffCalculator.ComputeFromFlatPoints(result.PreviousState.FlatPoints, eq.LatestFlatPoints);
	}
}
