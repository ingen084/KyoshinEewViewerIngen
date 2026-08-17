using Avalonia.Threading;
using System.Reactive.Concurrency;

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
	public static IScheduler Instance { get; } = new SynchronizationContextScheduler(new AvaloniaSynchronizationContext());
}
