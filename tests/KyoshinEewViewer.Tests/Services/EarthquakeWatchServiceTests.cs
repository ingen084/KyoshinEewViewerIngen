using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series.Earthquake.Models;
using KyoshinEewViewer.Series.Earthquake.Services;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.Services.TelegramPublishers;
using KyoshinEewViewer.Services.TelegramPublishers.Dmdata;
using KyoshinEewViewer.Tests.Services.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.ComponentModel;
using System.Reactive.Concurrency;
using System.Text;

namespace KyoshinEewViewer.Tests.Services;

/// <summary>
/// 電文が並行して届いたときの地震情報の一覧の更新を検証する
/// </summary>
public sealed class EarthquakeWatchServiceTests : IDisposable
{
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

	private readonly MockTelegramPublisher _publisher = new()
	{
		SupportedCategories = [InformationCategory.Earthquake, InformationCategory.Tsunami],
	};
	/// <summary>
	/// UI スレッドの代わりに一覧を変更する専用のスレッド
	/// </summary>
	private readonly EventLoopScheduler _uiScheduler = new();
	private readonly TelegramProvideService _provider;
	private readonly DmdataRedundantTelegramPublisher _dmdata;
	private readonly EarthquakeWatchService _service;

	public EarthquakeWatchServiceTests()
	{
		var services = new Mock<IServiceProvider>();
		services.Setup(s => s.GetService(typeof(MockTelegramPublisher))).Returns(_publisher);
		_provider = new TelegramProvideService(NullLogger<TelegramProvideService>.Instance, services.Object);

		var config = new KyoshinEewViewerConfiguration();
		_dmdata = new DmdataRedundantTelegramPublisher(
			NullLogger<DmdataRedundantTelegramPublisher>.Instance,
			config,
			new InformationCacheService(NullLogger<InformationCacheService>.Instance));
		_service = new EarthquakeWatchService(NullLogger<EarthquakeWatchService>.Instance, config, _provider, _dmdata, _uiScheduler);
	}

	[Fact(DisplayName = "並行して届いた電文で一覧に null が混入せず、すべてのイベントが登録される")]
	public async Task ConcurrentTelegrams_AllEventsAreRegistered()
	{
		await _provider.StartAsync([typeof(MockTelegramPublisher)]);

		const int count = 2000;
		var processed = 0;
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var subscription = _service.EarthquakeUpdated.Subscribe(_ =>
		{
			if (Interlocked.Increment(ref processed) == count)
				completed.TrySetResult();
		});

		var eventIds = Enumerable.Range(0, count).Select(i => i.ToString()).ToArray();
		Parallel.ForEach(eventIds, id => _publisher.SimulateTelegramArrival(InformationCategory.Earthquake, new TestTelegram(id, id)));
		await completed.Task.WaitAsync(Timeout);

		Assert.DoesNotContain(null, _service.Earthquakes);
		Assert.Equal(eventIds.Order(), _service.Earthquakes.Select(e => e.EventId).Order());
	}

	[Fact(DisplayName = "履歴の読み込み中に届いた電文は読み込みの完了後に処理される")]
	public async Task TelegramArrivedDuringHistoryLoading_IsProcessedAfterLoading()
	{
		await _provider.StartAsync([typeof(MockTelegramPublisher)]);

		var switched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var switchedSubscription = _service.SourceSwitched.Subscribe(_ => switched.TrySetResult());
		using var updatedSubscription = _service.EarthquakeUpdated.Subscribe(u =>
		{
			if (!u.IsBulkInserting)
				arrived.TrySetResult();
		});

		var bodyGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_publisher.SimulateHistoryTelegramArrival("テスト", InformationCategory.Earthquake, [new TestTelegram("history", "history", bodyGate: bodyGate.Task)]);
		_publisher.SimulateTelegramArrival(InformationCategory.Earthquake, new TestTelegram("arrived", "arrived"));

		Assert.Empty(_service.Earthquakes);

		bodyGate.SetResult();
		await Task.WhenAll(switched.Task, arrived.Task).WaitAsync(Timeout);

		Assert.Equal(["arrived", "history"], _service.Earthquakes.Select(e => e.EventId));
	}

	[Fact(DisplayName = "地震情報の処理中でも津波情報は次の購読者へすぐに届く")]
	public async Task TsunamiTelegramDuringHistoryLoading_ReachesNextSubscriberImmediately()
	{
		var received = new List<Telegram>();
		_provider.Subscribe(
			InformationCategory.Tsunami,
			(_, _) => Task.CompletedTask,
			t =>
			{
				received.Add(t);
				return Task.CompletedTask;
			},
			_ => { });
		await _provider.StartAsync([typeof(MockTelegramPublisher)]);

		var bodyGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		_publisher.SimulateHistoryTelegramArrival("テスト", InformationCategory.Earthquake, [new TestTelegram("history", "history", bodyGate: bodyGate.Task)]);

		var tsunami = new TestTelegram("tsunami", "tsunami", "津波情報a");
		_publisher.SimulateTelegramArrival(InformationCategory.Tsunami, tsunami);

		Assert.Same(tsunami, Assert.Single(received));
		bodyGate.SetResult();
	}

	[Fact(DisplayName = "地震情報の一覧と各イベントの変更は UI スレッドで行われる")]
	public async Task EarthquakeList_IsUpdatedOnUiThread()
	{
		await _provider.StartAsync([typeof(MockTelegramPublisher)]);
		var uiThreadId = await RunOnUiThreadAsync(() => Environment.CurrentManagedThreadId);

		var changedThreadIds = new List<int>();
		void Record() { lock (changedThreadIds) changedThreadIds.Add(Environment.CurrentManagedThreadId); }
		void OnEventChanged(object? sender, PropertyChangedEventArgs e) => Record();
		_service.Earthquakes.CollectionChanged += (_, e) =>
		{
			Record();
			foreach (var eq in e.NewItems?.OfType<EarthquakeEvent>() ?? [])
			{
				eq.PropertyChanged += OnEventChanged;
				eq.Fragments.CollectionChanged += (_, _) => Record();
			}
		};

		var switched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var switchedSubscription = _service.SourceSwitched.Subscribe(_ => switched.TrySetResult());
		_publisher.SimulateHistoryTelegramArrival("テスト", InformationCategory.Earthquake, [new TestTelegram("history", "history")]);
		await switched.Task.WaitAsync(Timeout);

		// 既存のイベントの更新と、新しいイベントの追加
		var arrived = 0;
		var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		using var updatedSubscription = _service.EarthquakeUpdated.Subscribe(u =>
		{
			if (!u.IsBulkInserting && Interlocked.Increment(ref arrived) == 2)
				completed.TrySetResult();
		});
		_publisher.SimulateTelegramArrival(InformationCategory.Earthquake, new TestTelegram("update", "history"));
		_publisher.SimulateTelegramArrival(InformationCategory.Earthquake, new TestTelegram("new", "new"));
		await completed.Task.WaitAsync(Timeout);

		Assert.Equal(["new", "history"], _service.Earthquakes.Select(e => e.EventId));
		lock (changedThreadIds)
		{
			Assert.NotEmpty(changedThreadIds);
			Assert.All(changedThreadIds, id => Assert.Equal(uiThreadId, id));
		}
	}

	[Fact(DisplayName = "過去の電文に処理できないものがあっても新しい電文は読み込み、失敗が続く場合は残りを打ち切る")]
	public async Task HistoryWithFailedTelegrams_SkipsFailuresUntilLimit()
	{
		await _provider.StartAsync([typeof(MockTelegramPublisher)]);
		var switched = 0;
		using var switchedSubscription = _service.SourceSwitched.Subscribe(_ => Interlocked.Increment(ref switched));

		// 1件だけ処理できない場合は、その電文だけを飛ばす
		_publisher.SimulateHistoryTelegramArrival("テスト", InformationCategory.Earthquake, [
			new TestTelegram("a", "a"),
			new TestTelegram("broken", "broken", fail: true),
			new TestTelegram("b", "b"),
		]);
		await WaitUntilAsync(() => Volatile.Read(ref switched) == 1);
		Assert.Equal(["b", "a"], _service.Earthquakes.Select(e => e.EventId));

		// 続けて処理できない場合は、回線の問題などとみなして残りを読み込まない
		_publisher.SimulateHistoryTelegramArrival("テスト", InformationCategory.Earthquake, [
			new TestTelegram("c", "c"),
			new TestTelegram("broken1", "broken1", fail: true),
			new TestTelegram("broken2", "broken2", fail: true),
			new TestTelegram("d", "d"),
		]);
		await WaitUntilAsync(() => Volatile.Read(ref switched) == 2);
		Assert.Equal(["c"], _service.Earthquakes.Select(e => e.EventId));
	}

	private Task<T> RunOnUiThreadAsync<T>(Func<T> func)
	{
		var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
		_uiScheduler.Schedule(() => completion.SetResult(func()));
		return completion.Task;
	}

	private static async Task WaitUntilAsync(Func<bool> condition)
	{
		using var cts = new CancellationTokenSource(Timeout);
		while (!condition())
			await Task.Delay(10, cts.Token);
	}

	public void Dispose()
	{
		_dmdata.Dispose();
		_provider.Dispose();
		_uiScheduler.Dispose();
	}

	private sealed class TestTelegram(string key, string eventId, string title = "震度速報", Task? bodyGate = null, bool fail = false)
		: Telegram(key, title, "TEST", DateTime.UnixEpoch)
	{
		private readonly byte[] _body = Encoding.UTF8.GetBytes($"""
			<Report>
				<Control><Title>{title}</Title><Status>通常</Status></Control>
				<Head>
					<ReportDateTime>2026-01-01T09:00:00+09:00</ReportDateTime>
					<TargetDateTime>2026-01-01T08:58:00+09:00</TargetDateTime>
					<EventID>{eventId}</EventID>
					<InfoType>発表</InfoType>
				</Head>
				<Body>
					<Intensity>
						<Observation>
							<MaxInt>3</MaxInt>
							<Pref>
								<Name>テスト県</Name>
								<Code>1</Code>
								<Area><Name>テスト地方</Name><Code>1</Code></Area>
							</Pref>
						</Observation>
					</Intensity>
				</Body>
			</Report>
			""");

		public override async Task<Stream> GetBodyAsync()
		{
			if (bodyGate != null)
				await bodyGate;
			if (fail)
				throw new HttpRequestException("電文を取得できません");
			return new MemoryStream(_body);
		}

		public override void Cleanup() { }
	}
}
