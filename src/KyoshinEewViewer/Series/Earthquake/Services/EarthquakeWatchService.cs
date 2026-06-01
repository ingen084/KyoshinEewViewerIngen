using DmdataSharp.ApiResponses.V2.Parameters;
using DmdataSharp.Exceptions;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.Earthquake.Events;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels;
using KyoshinEewViewer.Services.ExtarnalPublishers.Axis.ApiModels.Message;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinMonitorLib;
using ReactiveUI;
using Sentry;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Earthquake.Services;

/// <summary>
/// 地震情報の更新を担う
/// </summary>
public class EarthquakeWatchService : ReactiveObject
{
	private readonly string[] _targetTitles = ["震度速報", "震源に関する情報", "震源・震度に関する情報", "顕著な地震の震源要素更新のお知らせ", "長周期地震動に関する観測情報"];

	private const string AxisEarthquakeChannel = "jmx-seismology";

	public EarthquakeStationParameterResponse? Stations { get; private set; }
	public ObservableCollection<EarthquakeEvent> Earthquakes { get; } = [];

	private readonly Subject<EarthquakeUpdate> _earthquakeUpdatedSubject = new();
	private readonly Subject<Unit> _failedSubject = new();
	private readonly Subject<Unit> _sourceSwitchingSubject = new();
	private readonly Subject<string> _sourceSwitchedSubject = new();

	/// <summary>
	/// 地震情報が更新された際に通知される
	/// </summary>
	public IObservable<EarthquakeUpdate> EarthquakeUpdated => _earthquakeUpdatedSubject;
	/// <summary>
	/// 全ての受信元で接続に失敗した際に通知される
	/// </summary>
	public IObservable<Unit> Failed => _failedSubject;
	/// <summary>
	/// 受信元の切り替えが開始された際に通知される
	/// </summary>
	public IObservable<Unit> SourceSwitching => _sourceSwitchingSubject;
	/// <summary>
	/// 受信元の切り替えが完了した際に通知される(引数は受信元名)
	/// </summary>
	public IObservable<string> SourceSwitched => _sourceSwitchedSubject;

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Config { get; }
	private AxisInformationProvider AxisInformationProvider { get; }

	public EarthquakeWatchService(
		ILogManager logManager,
		KyoshinEewViewerConfiguration config,
		TelegramProvideService telegramProvider,
		DmdataRedundantTelegramPublisher dmdata,
		AxisInformationProvider axisInformationProvider)
	{
		SplatRegistrations.RegisterLazySingleton<EarthquakeWatchService>();

		Logger = logManager.GetLogger<EarthquakeWatchService>();
		Config = config;
		AxisInformationProvider = axisInformationProvider;

		telegramProvider.Subscribe(
			InformationCategory.Earthquake,
			async (s, t) =>
			{
				_sourceSwitchingSubject.OnNext(Unit.Default);

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
					_earthquakeUpdatedSubject.OnNext(new EarthquakeUpdate(eq, IsBulkInserting: true, IsDryRun: false, Fragment: null, PreviousMaxIntensity: null));
				_sourceSwitchedSubject.OnNext(s);
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
					_failedSubject.OnNext(Unit.Default);
				else
					_sourceSwitchingSubject.OnNext(Unit.Default);
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

		// AXIS 経路: AxisInformationProvider が単一の WebSocket でメッセージを配信するので
		// jmx-seismology チャンネルだけをこちらで購読する。EEW 側は KyoshinMonitor 系で別途購読される
		AxisInformationProvider.MessageReceived += OnAxisMessageReceived;
		Config.Axis.WhenAnyValue(x => x.Jwt, x => x.Enable).Subscribe(_ => TryActivateAxisEarthquake());
		TryActivateAxisEarthquake();
	}

	private void TryActivateAxisEarthquake()
	{
		if (!Config.Axis.Enable)
			return;

		if (string.IsNullOrWhiteSpace(Config.Axis.Jwt))
			return;
		try
		{
			if (!AxisJwtPayload.Parse(Config.Axis.Jwt).Channels.Contains(AxisEarthquakeChannel))
				return;
		}
		catch (Exception ex)
		{
			Logger.LogWarning(ex, "AXIS JWT のチャンネル抽出に失敗しました");
			return;
		}

		AxisInformationProvider.Initialize();
	}

	private async void OnAxisMessageReceived(AxisWebSocketMessage message)
	{
		try
		{
			if (message.Channel != AxisEarthquakeChannel)
				return;

			EarthquakeMessage? msg;
			try
			{
				msg = message.Message.Deserialize<EarthquakeMessage>();
			}
			catch (Exception ex)
			{
				Logger.LogWarning(ex, "AXIS地震情報のJSONデシリアライズに失敗しました");
				return;
			}
			if (msg?.Control == null || msg.Head == null)
				return;

			if (!_targetTitles.Contains(msg.Control.Title))
				return;

			if (!Config.Axis.ReceiveTraining && msg.Control.Status == "訓練")
				return;

			Logger.LogDebug($"AXIS地震情報を受信しました: {msg.Control.Title} {msg.Head.EventID}");
			await ProcessAxisInformation(msg);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "AXIS地震情報の処理中に例外が発生しました");
		}
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
				_earthquakeUpdatedSubject.OnNext(new EarthquakeUpdate(eq, IsBulkInserting: false, IsDryRun: false, Fragment: null, PreviousMaxIntensity: null));
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

			// dryRun は表示確認用の一時イベントとして処理し、既存イベントを書き換えない
			var existing = Earthquakes.FirstOrDefault(e => e.EventId == report.Head.EventId);
			var eq = dryRun
				? new EarthquakeEvent(report.Head.EventId)
				: existing ?? new EarthquakeEvent(report.Head.EventId);
			var isCreated = existing == null || dryRun;

			// 情報更新前の震度
			var prevInt = eq.Intensity;

			// 情報を処理
			var fragment = eq.ProcessTelegram(telegram, report, Stations?.Items);
			// 取消・冪等化・等価判定で変化なし: 既存イベントなら通知のみスキップ、新規イベントなら登録もスキップ
			if (fragment == null)
				return isCreated ? null : eq;

			// ここで初めて Earthquakes へ登録（空の EarthquakeEvent を残さないため）
			if (isCreated && !dryRun)
				Earthquakes.Insert(0, eq);

			if (!hideNotice)
				_earthquakeUpdatedSubject.OnNext(new EarthquakeUpdate(eq, IsBulkInserting: false, IsDryRun: dryRun, Fragment: fragment, PreviousMaxIntensity: isCreated ? null : prevInt));
			return eq;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "デシリアライズ時に例外が発生しました");
			return null;
		}
	}

	public Task<EarthquakeEvent?> ProcessAxisInformation(EarthquakeMessage message)
	{
		try
		{
			if (!_targetTitles.Contains(message.Control.Title))
				return Task.FromResult<EarthquakeEvent?>(null);

			var eq = Earthquakes.FirstOrDefault(e => e.EventId == message.Head.EventID);
			var isCreated = eq == null;
			eq ??= new EarthquakeEvent(message.Head.EventID);

			var prevInt = eq.Intensity;
			var fragment = eq.ProcessAxisMessage(message, Stations?.Items);
			if (fragment == null)
				return Task.FromResult<EarthquakeEvent?>(isCreated ? null : eq);

			if (isCreated)
				Earthquakes.Insert(0, eq);

			_earthquakeUpdatedSubject.OnNext(new EarthquakeUpdate(eq, IsBulkInserting: false, IsDryRun: false, Fragment: fragment, PreviousMaxIntensity: isCreated ? null : prevInt));
			return Task.FromResult<EarthquakeEvent?>(eq);
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "AXIS地震情報の処理中に例外が発生しました");
			return Task.FromResult<EarthquakeEvent?>(null);
		}
	}
}
