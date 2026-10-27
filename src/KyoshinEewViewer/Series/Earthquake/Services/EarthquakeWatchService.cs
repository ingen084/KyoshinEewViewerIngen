using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DmdataSharp.ApiResponses.V2.Parameters;
using DmdataSharp.Exceptions;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.Earthquake.Events;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinMonitorLib;
using Sentry;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace KyoshinEewViewer.Series.Earthquake.Services;

/// <summary>
/// 地震情報の更新を担う
/// </summary>
public class EarthquakeWatchService : ObservableObject
{
	private readonly string[] _targetTitles = ["震度速報", "震源に関する情報", "震源・震度に関する情報", "顕著な地震の震源要素更新のお知らせ", "長周期地震動に関する観測情報"];

	public EarthquakeStationParameterResponse? Stations { get; private set; }
	/// <summary>
	/// 地震情報の一覧<br/>
	/// 画面にバインドされるため、UIスレッドでのみ変更する
	/// </summary>
	public ObservableCollection<EarthquakeEvent> Earthquakes { get; } = [];

	/// <summary>
	/// 地震情報の一覧を更新する処理を1つずつ実行するためのロック<br/>
	/// 受信元の切り替えによる一覧の作り直しと、受信した電文の処理が混ざらないようにする
	/// </summary>
	private readonly SemaphoreSlim _processLock = new(1, 1);

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

	public EarthquakeWatchService(
		ILogger<EarthquakeWatchService> logger,
		KyoshinEewViewerConfiguration config,
		TelegramProvideService telegramProvider,
		DmdataRedundantTelegramPublisher dmdata)
	{
		Logger = logger;
		Config = config;

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

				await _processLock.WaitAsync();
				try
				{
					await Dispatcher.UIThread.InvokeAsync(() => Earthquakes.Clear());
					foreach (var h in t.OrderBy(h => h.ArrivalTime).ToArray())
					{
						try
						{
							await ProcessInformationCore(h, dryRun: false, hideNotice: true);
						}
						catch (Exception ex)
						{
							Logger.LogError(ex, "キャッシュ破損疑いのため削除します");
							try
							{
								// キャッシュ破損時用
								h.Cleanup();
								await ProcessInformationCore(h, dryRun: false, hideNotice: true);
							}
							catch (Exception ex2)
							{
								// その他のエラー発生時は残りの電文の処理を中断させる
								Logger.LogError(ex2, "初回電文取得中に問題が発生しました");
								break;
							}
						}
					}
					var earthquakes = await Dispatcher.UIThread.InvokeAsync(() =>
					{
						// 電文データがない(震源情報しかないなどの)データを削除する
						foreach (var eq in Earthquakes.Where(e => e.Fragments.All(f => f is not IntensityInformationFragment and not HypocenterAndIntensityInformationFragment)).ToArray())
							Earthquakes.Remove(eq);
						return Earthquakes.ToArray();
					});

					foreach (var eq in earthquakes)
						_earthquakeUpdatedSubject.OnNext(new EarthquakeUpdate(eq, IsBulkInserting: true, IsDryRun: false, Fragment: null, PreviousMaxIntensity: null));
					_sourceSwitchedSubject.OnNext(s);
				}
				finally
				{
					_processLock.Release();
				}
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
	}

	public async Task ProcessTsunamiInformation(Telegram telegram, bool hideNotice = false)
	{
		await _processLock.WaitAsync();
		try
		{
			await using var stream = await telegram.GetBodyAsync();
			using var report = new JmaXmlDocument(stream);
			if (report.Control.Title != "津波警報・注意報・予報a")
				return;

			var fragments = EarthquakeInformationFragment.CreateFromTsunamiJmxXmlDocument(telegram, report);
			foreach (var (eventId, fragment) in fragments)
			{
				// TODO 作成できるようにしておいた方がよさそう
				var eq = await Dispatcher.UIThread.InvokeAsync(() => Earthquakes.FirstOrDefault(e => e.EventId == eventId));
				if (eq == null)
				{
					Logger.LogWarning("イベントID {EventId} が見つからなかったため津波情報による震源情報の更新を行いませんでした。", eventId);
					continue;
				}
				eq.AddFragment(fragment);
				if (!hideNotice)
					_earthquakeUpdatedSubject.OnNext(new EarthquakeUpdate(eq, IsBulkInserting: false, IsDryRun: false, Fragment: null, PreviousMaxIntensity: null));
			}
		}
		finally
		{
			_processLock.Release();
		}
	}

	public async Task<EarthquakeEvent?> ProcessInformation(Telegram telegram, bool dryRun = false, bool hideNotice = false)
	{
		await _processLock.WaitAsync();
		try
		{
			return await ProcessInformationCore(telegram, dryRun, hideNotice);
		}
		finally
		{
			_processLock.Release();
		}
	}

	/// <summary>
	/// 電文を処理して地震情報の一覧に反映する<br/>
	/// 呼び出し元で <see cref="_processLock"/> を取得しておくこと
	/// </summary>
	private async Task<EarthquakeEvent?> ProcessInformationCore(Telegram telegram, bool dryRun, bool hideNotice)
	{
		await using var stream = await telegram.GetBodyAsync();
		using var report = new JmaXmlDocument(stream);

		try
		{
			// サポート外であれば見なかったことにする
			if (!_targetTitles.Contains(report.Control.Title))
				return null;

			var eventId = report.Head.EventId;
			EarthquakeEvent eq;
			bool isCreated;
			if (dryRun)
			{
				eq = new EarthquakeEvent(eventId);
				isCreated = true;
			}
			else
			{
				// 保存されている Earthquake インスタンスを抜き出し、なければ一覧に追加する
				(eq, isCreated) = await Dispatcher.UIThread.InvokeAsync(() =>
				{
					if (Earthquakes.FirstOrDefault(e => e.EventId == eventId) is { } found)
						return (found, false);
					var created = new EarthquakeEvent(eventId);
					Earthquakes.Insert(0, created);
					return (created, true);
				});
			}

			// 情報更新前の震度
			var prevInt = eq.Intensity;

			// 情報を処理
			var fragment = eq.ProcessTelegram(telegram, report);
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
}
