using System;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Core.Events
{
	/// <summary>
	/// スレッドプールで購読処理を実行する
	/// </summary>
	public class BackgroundEventSubscription : EventSubscription
	{
		public BackgroundEventSubscription(DelegateReference actionReference)
			: base(actionReference)
		{
		}
		public override void InvokeAction(Action action)
			=> Task.Run(action);
	}

	/// <summary>
	/// スレッドプールで購読処理を実行する
	/// </summary>
	public class BackgroundEventSubscription<TPayload> : EventSubscription<TPayload>
	{
		public BackgroundEventSubscription(DelegateReference actionReference, DelegateReference filterReference)
			: base(actionReference, filterReference)
		{
		}
		public override void InvokeAction(Action<TPayload?> action, TPayload? argument)
			=> Task.Run(() => action(argument));
	}
}
