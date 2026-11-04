using Avalonia.Threading;
using System.Reactive.Concurrency;
using System.Threading;

namespace KyoshinEewViewer.Core;

/// <summary>
/// System.Reactive のストリームを UI スレッドへマーシャリングするためのスケジューラ。
/// ReactiveUI の RxSchedulers.MainThreadScheduler の代替。
/// </summary>
public static class UiScheduler
{
	/// <summary>
	/// Avalonia の UI スレッドで実行するスケジューラ
	/// </summary>
	public static IScheduler Instance { get; } = new SynchronizationContextScheduler(new UiThreadSynchronizationContext());

	/// <summary>
	/// 処理を送る時点の UI スレッドのディスパッチャへ処理を送る<br/>
	/// UIテストではテストごとにディスパッチャが作り直されるため、生成時のディスパッチャに固定しない
	/// </summary>
	private sealed class UiThreadSynchronizationContext : SynchronizationContext
	{
		public override void Post(SendOrPostCallback d, object? state)
			=> Dispatcher.UIThread.Post(() => d(state));

		public override void Send(SendOrPostCallback d, object? state)
			=> Dispatcher.UIThread.Invoke(() => d(state));

		public override SynchronizationContext CreateCopy() => this;
	}
}
