using System;
using System.Threading;

namespace KyoshinEewViewer.Core.Events
{
	/// <summary>
	/// UIスレッドで購読処理を実行させる
	/// </summary>
	public class DispatcherEventSubscription : EventSubscription
	{
		private readonly SynchronizationContext syncContext;

		public DispatcherEventSubscription(DelegateReference actionReference, SynchronizationContext context)
			: base(actionReference)
		{
			syncContext = context;
		}
		public override void InvokeAction(Action action)
			=> syncContext.Post((o) => action(), null);
	}

	/// <summary>
	/// UIスレッドで購読処理を実行させる
	/// </summary>
	public class DispatcherEventSubscription<TPayload> : EventSubscription<TPayload>
	{
		private readonly SynchronizationContext syncContext;

		public DispatcherEventSubscription(DelegateReference actionReference, DelegateReference filterReference, SynchronizationContext context)
			: base(actionReference, filterReference)
		{
			syncContext = context;
		}
		public override void InvokeAction(Action<TPayload?> action, TPayload? argument)
			=> syncContext.Post((o) => action((TPayload)o), argument);
	}
}
