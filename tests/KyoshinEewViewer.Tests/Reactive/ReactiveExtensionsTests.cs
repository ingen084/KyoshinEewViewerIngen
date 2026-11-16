using Avalonia;
using KyoshinEewViewer.Core;
using System.ComponentModel;
using System.Reactive.Linq;

namespace KyoshinEewViewer.Tests.Reactive;

public sealed class ReactiveExtensionsTests
{
	[Fact(DisplayName = "プロパティの変更を監視でき、購読を解除するとイベントハンドラーが外れる")]
	public void ObservePropertyChanged_NotifiesAndUnsubscribes()
	{
		var source = new Node { Value = 1 };
		var received = new List<int>();

		var subscription = source.ObservePropertyChanged(x => x.Value).Subscribe(received.Add);
		Assert.Equal([1], received);
		Assert.Equal(1, source.HandlerCount);

		source.Value = 2;
		source.Name = "対象外のプロパティ";
		source.Value = 3;
		Assert.Equal([1, 2, 3], received);

		subscription.Dispose();
		Assert.Equal(0, source.HandlerCount);
		source.Value = 4;
		Assert.Equal([1, 2, 3], received);

		var withoutCurrent = new List<int>();
		using (source.ObservePropertyChanged(x => x.Value, pushCurrentValueOnSubscribe: false).Subscribe(withoutCurrent.Add))
		{
			Assert.Empty(withoutCurrent);
			source.Value = 5;
			Assert.Equal([5], withoutCurrent);
		}
		Assert.Equal(0, source.HandlerCount);
	}

	[Fact(DisplayName = "入れ子のプロパティは途中のオブジェクトが差し替わると監視し直し、解除すると全階層のハンドラーが外れる")]
	public void ObservePropertyChanged_Nested_FollowsReplacement()
	{
		var firstChild = new Node { Value = 10 };
		var secondChild = new Node { Value = 20 };
		var root = new Node { Child = firstChild };
		var received = new List<int>();

		var subscription = root.ObservePropertyChanged(x => x.Child, x => x.Value).Subscribe(received.Add);
		firstChild.Value = 11;
		Assert.Equal([10, 11], received);

		root.Child = secondChild;
		Assert.Equal(0, firstChild.HandlerCount);
		firstChild.Value = 12;
		secondChild.Value = 21;
		Assert.Equal([10, 11, 20, 21], received);

		// 途中が null の間は値を流さず、再設定されると監視を再開する
		root.Child = null;
		Assert.Equal(0, secondChild.HandlerCount);
		secondChild.Value = 22;
		Assert.Equal([10, 11, 20, 21], received);
		root.Child = secondChild;
		Assert.Equal([10, 11, 20, 21, 22], received);

		subscription.Dispose();
		Assert.Equal(0, root.HandlerCount);
		Assert.Equal(0, secondChild.HandlerCount);

		// 3階層
		var grandChild = new Node { Value = 100 };
		var deepRoot = new Node { Child = new Node { Child = grandChild } };
		var deepReceived = new List<int>();
		using (deepRoot.ObservePropertyChanged(x => x.Child, x => x.Child, x => x.Value).Subscribe(deepReceived.Add))
		{
			grandChild.Value = 101;
			deepRoot.Child = new Node { Child = new Node { Value = 200 } };
			grandChild.Value = 102;
			Assert.Equal([100, 101, 200], deepReceived);
		}
		Assert.Equal(0, deepRoot.HandlerCount);
		Assert.Equal(0, grandChild.HandlerCount);
	}

	[Fact(DisplayName = "AvaloniaObject のプロパティを監視できる")]
	public void ObservePropertyChanged_AvaloniaObject()
	{
		var target = new SampleAvaloniaObject();
		var received = new List<double>();

		using (target.ObservePropertyChanged(x => x.Size).Subscribe(received.Add))
			target.Size = 2.5;

		target.Size = 3;
		Assert.Equal([0, 2.5], received);
	}

	[Fact(DisplayName = "SubscribeLoggingErrors はハンドラーが例外を投げても発行元へ伝播させず、以後の値も受け取る")]
	public void SubscribeLoggingErrors_ContinuesAfterException()
	{
		// 直接購読する場合と、オペレーターを挟む場合の両方で確認する
		var chains = new Func<Node, IObservable<int>>[]
		{
			source => source.ObservePropertyChanged(x => x.Value),
			source => source.ObservePropertyChanged(x => x.Value).Where(x => x >= 0),
			source => Observable.CombineLatest(
				source.ObservePropertyChanged(x => x.Value),
				source.ObservePropertyChanged(x => x.Name),
				(value, _) => value),
		};

		foreach (var chain in chains)
		{
			var source = new Node();
			var received = new List<int>();

			using var subscription = chain(source).SubscribeLoggingErrors(x =>
			{
				received.Add(x);
				if (x == 1)
					throw new InvalidOperationException("テスト用の例外");
			});

			source.Value = 1;
			source.Value = 2;
			Assert.Equal([0, 1, 2], received);
		}
	}

	[Fact(DisplayName = "ThrottleLast は最初の値から一定時間後に最後の値だけを流し、値がない間はタイマーを動かさない")]
	public void ThrottleLast_EmitsLastValueAfterInterval()
	{
		var timeProvider = new SingleTimerTimeProvider();
		var source = new Node();
		var received = new List<int>();
		var interval = TimeSpan.FromMilliseconds(100);

		var subscription = source.ObservePropertyChanged(x => x.Value, pushCurrentValueOnSubscribe: false)
			.ThrottleLast(interval, timeProvider)
			.Subscribe(received.Add);
		Assert.Null(timeProvider.DueTime);

		source.Value = 1;
		source.Value = 2;
		source.Value = 3;
		Assert.Equal(interval, timeProvider.DueTime);
		Assert.Empty(received);

		timeProvider.Fire();
		Assert.Equal([3], received);
		Assert.Null(timeProvider.DueTime);

		// 発火後に値が届かなければ何も流さない
		timeProvider.Fire();
		Assert.Equal([3], received);

		source.Value = 4;
		Assert.Equal(interval, timeProvider.DueTime);
		timeProvider.Fire();
		Assert.Equal([3, 4], received);

		subscription.Dispose();
		Assert.True(timeProvider.IsTimerDisposed);
		Assert.Equal(0, source.HandlerCount);
	}

	private sealed class Node : INotifyPropertyChanged
	{
		private PropertyChangedEventHandler? _handlers;
		public event PropertyChangedEventHandler? PropertyChanged
		{
			add => _handlers += value;
			remove => _handlers -= value;
		}
		public int HandlerCount => _handlers?.GetInvocationList().Length ?? 0;

		public int Value
		{
			get;
			set {
				field = value;
				_handlers?.Invoke(this, new(nameof(Value)));
			}
		}

		public string? Name
		{
			get;
			set {
				field = value;
				_handlers?.Invoke(this, new(nameof(Name)));
			}
		}

		public Node? Child
		{
			get;
			set {
				field = value;
				_handlers?.Invoke(this, new(nameof(Child)));
			}
		}
	}

	private sealed class SampleAvaloniaObject : AvaloniaObject
	{
		public static readonly StyledProperty<double> SizeProperty =
			AvaloniaProperty.Register<SampleAvaloniaObject, double>(nameof(Size));

		public double Size
		{
			get => GetValue(SizeProperty);
			set => SetValue(SizeProperty, value);
		}
	}

	/// <summary>
	/// 1つのタイマーだけを扱い、テストから任意のタイミングで発火させる
	/// </summary>
	private sealed class SingleTimerTimeProvider : TimeProvider
	{
		private TimerCallback? _callback;

		/// <summary>
		/// タイマーが予約されている場合の発火までの時間
		/// </summary>
		public TimeSpan? DueTime { get; private set; }
		public bool IsTimerDisposed { get; private set; }

		public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
		{
			_callback = callback;
			var timer = new Timer(this);
			timer.Change(dueTime, period);
			return timer;
		}

		public void Fire()
		{
			DueTime = null;
			_callback?.Invoke(null);
		}

		private sealed class Timer(SingleTimerTimeProvider owner) : ITimer
		{
			public bool Change(TimeSpan dueTime, TimeSpan period)
			{
				owner.DueTime = dueTime == Timeout.InfiniteTimeSpan ? null : dueTime;
				return true;
			}

			public void Dispose() => owner.IsTimerDisposed = true;
			public ValueTask DisposeAsync()
			{
				Dispose();
				return ValueTask.CompletedTask;
			}
		}
	}
}
