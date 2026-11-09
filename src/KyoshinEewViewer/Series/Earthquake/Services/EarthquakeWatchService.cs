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
using System.Reactive.Concurrency;
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

	/// <summary>
	/// 過去の電文の読み込みで、処理できなかった電文がこの数に達したら残りの読み込みを打ち切る<br/>
	/// 読み込み中は新しく届いた電文を待たせるため、回線の問題などで取得できない状態で読み込みを続けないようにする
	/// </summary>
	private const int MaxHistoryFailureCount = 2;

	public EarthquakeStationParameterResponse? Stations { get; private set; }
	/// <summary>
	/// 地震情報の一覧<br/>
	/// 画面にバインドされるため、一覧と各イベントの変更は <see cref="UpdateScheduler"/> で行う
	/// </summary>
	public ObservableCollection<EarthquakeEvent> Earthquakes { get; } = [];
	/// <summary>
	/// 地震情報の一覧と各イベントを変更する処理を実行するスケジューラ (本番では UI スレッド)
	/// </summary>
	private IScheduler UpdateScheduler { get; }

	private readonly Subject<EarthquakeUpdate> _earthquakeUpdatedSubject = new();
	private readonly Subject<Unit> _failedSubject = new();
	private readonly Subject<Unit> _sourceSwitchingSubject = new();
	private readonly Subject<string> _sourceSwitchedSubject = new();
	/// <summary>
	/// 地震情報の一覧を更新する処理を直列化する
	/// </summary>
	private readonly SemaphoreSlim _processingLock = new(1, 1);

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
		: this(logger, config, telegramProvider, dmdata, UiScheduler.Instance)
	{
	}

	/// <summary>
	/// 一覧を変更するスケジューラを指定して生成する<br/>
	/// UI スレッドのないテストで差し替えるために使う
	/// </summary>
	internal EarthquakeWatchService(
		ILogger<EarthquakeWatchService> logger,
		KyoshinEewViewerConfiguration config,
		TelegramProvideService telegramProvider,
		DmdataRedundantTelegramPublisher dmdata,
		IScheduler updateScheduler)
	{
		Logger = logger;
		Config = config;
		UpdateScheduler = updateScheduler;

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

				await RunExclusiveAsync(async () =>
				{
					await RunOnUpdateSchedulerAsync(Earthquakes.Clear);
					var failureCount = 0;
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
								// 処理できなかった電文だけを飛ばし、より新しい電文の処理を続ける
								Logger.LogError(ex2, "初回電文取得中に問題が発生しました");
								if (++failureCount < MaxHistoryFailureCount)
									continue;
								Logger.LogWarning("過去の電文を{FailureCount}件処理できなかったため、残りの読み込みを中止します", failureCount);
								break;
							}
						}
					}
					var earthquakes = await RunOnUpdateSchedulerAsync(() =>
					{
						// 電文データがない(震源情報しかないなどの)データを削除する
						foreach (var eq in Earthquakes.Where(e => e.Fragments.All(f => f is not IntensityInformationFragment and not HypocenterAndIntensityInformationFragment)).ToArray())
							Earthquakes.Remove(eq);
						return Earthquakes.ToArray();
					});

					foreach (var eq in earthquakes)
						_earthquakeUpdatedSubject.OnNext(new EarthquakeUpdate(eq, IsBulkInserting: true, IsDryRun: false, Fragment: null, PreviousMaxIntensity: null));
					_sourceSwitchedSubject.OnNext(s);
				});
			},
			t => RunExclusiveAsync(async () =>
			{
				var trans = SentrySdk.StartTransaction("earthquake", "arrived");
				try
				{
					await ProcessInformation(t);
					trans.Finish();
				}
				catch (Exception ex)
				{
					Logger.LogWarning(ex, "地震情報の電文を処理できませんでした: {Key} {Title}", t.Key, t.Title);
					trans.Finish(ex);
				}
			}),
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
			t =>
			{
				// 購読者は順番に呼ばれるので、ここで処理を待つと津波情報の表示まで遅れる
				_ = RunExclusiveAsync(async () =>
				{
					try
					{
						await ProcessTsunamiInformation(t);
					}
					catch (Exception ex)
					{
						Logger.LogError(ex, "津波情報による震源情報の更新に失敗しました。");
					}
				});
				return Task.CompletedTask;
			},
			_ => { }
		);
	}

	private async Task RunExclusiveAsync(Func<Task> action)
	{
		await _processingLock.WaitAsync();
		try
		{
			await action();
		}
		finally
		{
			_processingLock.Release();
		}
	}

	/// <summary>
	/// 地震情報の一覧と各イベントを変更する処理を <see cref="UpdateScheduler"/> で実行し、完了を待つ
	/// </summary>
	private Task<T> RunOnUpdateSchedulerAsync<T>(Func<T> func)
	{
		// 完了後の電文の取得などを UI スレッドで続けないよう、継続は非同期に実行する
		var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
		UpdateScheduler.Schedule(() =>
		{
			try
			{
				completion.SetResult(func());
			}
			catch (Exception ex)
			{
				completion.SetException(ex);
			}
		});
		return completion.Task;
	}

	private Task RunOnUpdateSchedulerAsync(Action action)
		=> RunOnUpdateSchedulerAsync(() =>
		{
			action();
			return true;
		});

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
			var eq = await RunOnUpdateSchedulerAsync(() =>
			{
				var eq = Earthquakes.FirstOrDefault(e => e.EventId == eventId);
				eq?.AddFragment(fragment);
				return eq;
			});
			if (eq == null)
			{
				Logger.LogWarning("イベントID {EventId} が見つからなかったため津波情報による震源情報の更新を行いませんでした。", eventId);
				continue;
			}
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

			(EarthquakeEvent eq, EarthquakeInformationFragment? fragment, JmaIntensity? prevInt) Process()
			{
				var isCreated = false;
				// 保存されている Earthquake インスタンスを抜き出してくる
				var eq = Earthquakes.FirstOrDefault(e => e.EventId == report.Head.EventId);
				if (eq == null || dryRun)
				{
					eq = new EarthquakeEvent(report.Head.EventId);
					if (!dryRun)
						Earthquakes.Insert(0, eq);
					isCreated = true;
				}

				// 情報更新前の震度
				var prevInt = eq.Intensity;

				// 情報を処理
				var fragment = eq.ProcessTelegram(telegram, report);
				return (eq, fragment, isCreated ? null : prevInt);
			}

			// 確認用の読み込みは一覧に追加しないので、呼び出し元のスレッドで処理する
			var (eq, fragment, prevInt) = dryRun ? Process() : await RunOnUpdateSchedulerAsync(Process);
			if (!hideNotice)
				_earthquakeUpdatedSubject.OnNext(new EarthquakeUpdate(eq, IsBulkInserting: false, IsDryRun: dryRun, Fragment: fragment, PreviousMaxIntensity: prevInt));
			return eq;
		}
		catch (Exception ex)
		{
			Logger.LogError(ex, "デシリアライズ時に例外が発生しました");
			return null;
		}
	}
}
