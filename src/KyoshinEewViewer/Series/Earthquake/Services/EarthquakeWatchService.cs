using DmdataSharp.ApiResponses.V2.Parameters;
using DmdataSharp.Exceptions;
using KyoshinEewViewer.BufrParser;
using KyoshinEewViewer.BufrParser.Exceptions;
using KyoshinEewViewer.BufrParser.Ixac41;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.JmaXmlParser;
using KyoshinEewViewer.Series.Earthquake.Events;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinMonitorLib;
using ReactiveUI;
using Sentry;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.Earthquake.Services;

/// <summary>
/// 地震情報の更新を担う
/// </summary>
public class EarthquakeWatchService : ReactiveObject
{
	private readonly string[] _targetTitles = ["震度速報", "震源に関する情報", "震源・震度に関する情報", "顕著な地震の震源要素更新のお知らせ", "長周期地震動に関する観測情報"];

	// 推計震度分布図(IXAC41 BUFR電文)と地震イベントの発生時刻を突き合わせる際の許容誤差
	// 注意: 震央座標での突き合わせは行っていないため、近接時刻に複数の地震が発生した場合誤って紐付く可能性がある(既知の制約、将来課題)
	private static readonly TimeSpan EstimatedIntensityMatchTolerance = TimeSpan.FromSeconds(60);
	// バッファする推計震度分布図の上限件数
	private const int MaxPendingEstimatedIntensityReportsCount = 20;

	// IXAC41(推計震度分布図)は512KB超で分割配信されるため、サービスにつき1つの結合器で受信順に結合する
	// 同時多発地震で分割電文がインターリーブした場合は片方が破棄される可能性がある(低頻度、FragmentDiscardedでWarningログに記録)
	private readonly BufrFragmentAssembler _estimatedIntensityAssembler = new();
	// 発生時刻が一致する地震イベントがまだ存在しない推計震度分布図を一時的に保持する
	private readonly List<(DateTime OccurredJst, EstimatedSeismicIntensityReport Report)> _pendingEstimatedIntensityReports = [];

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

	public EarthquakeWatchService(
		ILogManager logManager,
		KyoshinEewViewerConfiguration config,
		TelegramProvideService telegramProvider,
		DmdataRedundantTelegramPublisher dmdata)
	{
		SplatRegistrations.RegisterLazySingleton<EarthquakeWatchService>();

		Logger = logManager.GetLogger<EarthquakeWatchService>();
		Config = config;

		_estimatedIntensityAssembler.FragmentDiscarded += discardedBytes =>
			Logger.LogWarning($"推計震度分布図(BUFR)の未完成な分割電文 {discardedBytes} バイトを破棄しました");

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
		// IXAC41(推計震度分布図)はBUFRバイナリ電文でありJmaXmlDocumentとして解釈できないため専用処理へ分岐する
		if (telegram.RawId == "IXAC41")
			return await ProcessEstimatedIntensityInformation(telegram, dryRun, hideNotice);

		await using var stream = await telegram.GetBodyAsync();
		using var report = new JmaXmlDocument(stream);

		try
		{
			// サポート外であれば見なかったことにする
			if (!_targetTitles.Contains(report.Control.Title))
				return null;

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

			// バッファ済みの推計震度分布図があれば、発生時刻が近いものを適用する(dryRunでは一時的なイベントのためバッファを消費しない)
			if (!dryRun)
				TryApplyPendingEstimatedIntensityReports(eq);

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

	/// <summary>
	/// IXAC41(推計震度分布図 BUFR電文)を処理する
	/// </summary>
	private async Task<EarthquakeEvent?> ProcessEstimatedIntensityInformation(Telegram telegram, bool dryRun, bool hideNotice)
	{
		await using var stream = await telegram.GetBodyAsync();
		using var memoryStream = new MemoryStream();
		await stream.CopyToAsync(memoryStream);

		// 512KB超の電文は分割配信されるため、結合が完了するまでは処理を継続しない
		if (!_estimatedIntensityAssembler.TryAppend(memoryStream.ToArray(), out var completeMessage))
		{
			Logger.LogDebug("推計震度分布図(BUFR)の分割電文を受信しました。結合完了を待機します");
			return null;
		}

		EstimatedSeismicIntensityReport report;
		try
		{
			report = EstimatedSeismicIntensityReport.Parse(completeMessage!);
		}
		catch (BufrParseException ex)
		{
			Logger.LogWarning(ex, "推計震度分布図(BUFR)電文の解析に失敗しました");
			return null;
		}

		// テスト用の一時的な読み込み(dryRun)ではイベントへの反映やバッファへの蓄積は行わない
		if (dryRun)
			return null;

		// 電文の発生時刻(UTC)をJSTへ変換する
		var occurredJst = report.OriginTimeUtc.AddHours(9);

		// 発生時刻が近い(±60秒以内)地震イベントを検索する
		// 注意: 震央座標での突き合わせは行っていないため、近接時刻に複数の地震が発生した場合誤って紐付く可能性がある(既知の制約、将来課題)
		var eq = FindNearestEarthquakeEvent(occurredJst);
		if (eq == null)
		{
			BufferPendingEstimatedIntensityReport(occurredJst, report);
			Logger.LogInfo($"推計震度分布図に対応する地震イベントが見つからなかったためバッファしました 発生時刻(JST): {occurredJst:yyyy/MM/dd HH:mm:ss}");
			return null;
		}

		eq.EstimatedIntensityDistribution = report;
		if (!hideNotice)
			_earthquakeUpdatedSubject.OnNext(new EarthquakeUpdate(eq, IsBulkInserting: false, IsDryRun: false, Fragment: null, PreviousMaxIntensity: null));
		return eq;
	}

	/// <summary>
	/// 発生時刻(JST)から最も近い地震イベントを許容誤差内で検索する
	/// </summary>
	private EarthquakeEvent? FindNearestEarthquakeEvent(DateTime occurredJst)
		=> Earthquakes
			.Where(e => Math.Abs((e.Time - occurredJst).TotalSeconds) <= EstimatedIntensityMatchTolerance.TotalSeconds)
			.OrderBy(e => Math.Abs((e.Time - occurredJst).TotalSeconds))
			.FirstOrDefault();

	/// <summary>
	/// 対応する地震イベントが見つからなかった推計震度分布図をバッファに追加する。上限を超えたら古いものから破棄する
	/// </summary>
	private void BufferPendingEstimatedIntensityReport(DateTime occurredJst, EstimatedSeismicIntensityReport report)
	{
		_pendingEstimatedIntensityReports.Add((occurredJst, report));
		if (_pendingEstimatedIntensityReports.Count > MaxPendingEstimatedIntensityReportsCount)
			_pendingEstimatedIntensityReports.RemoveRange(0, _pendingEstimatedIntensityReports.Count - MaxPendingEstimatedIntensityReportsCount);
	}

	/// <summary>
	/// バッファ済みの推計震度分布図から、指定した地震イベントの発生時刻に最も近いものを適用する
	/// </summary>
	private void TryApplyPendingEstimatedIntensityReports(EarthquakeEvent eq)
	{
		if (_pendingEstimatedIntensityReports.Count == 0)
			return;

		var nearestIndex = -1;
		var nearestDiffSeconds = double.MaxValue;
		for (var i = 0; i < _pendingEstimatedIntensityReports.Count; i++)
		{
			var diffSeconds = Math.Abs((eq.Time - _pendingEstimatedIntensityReports[i].OccurredJst).TotalSeconds);
			if (diffSeconds > EstimatedIntensityMatchTolerance.TotalSeconds || diffSeconds >= nearestDiffSeconds)
				continue;
			nearestIndex = i;
			nearestDiffSeconds = diffSeconds;
		}
		if (nearestIndex < 0)
			return;

		eq.EstimatedIntensityDistribution = _pendingEstimatedIntensityReports[nearestIndex].Report;
		_pendingEstimatedIntensityReports.RemoveAt(nearestIndex);
		Logger.LogInfo($"バッファ済みの推計震度分布図をイベント {eq.EventId} に適用しました");
	}
}
