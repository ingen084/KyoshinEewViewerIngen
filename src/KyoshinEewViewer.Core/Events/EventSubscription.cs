using System;

namespace KyoshinEewViewer.Core.Events
{
	/// <summary>
	/// イベントに対しての購読
	/// </summary>
	public class EventSubscription : IEventSubscription
	{
		private readonly DelegateReference _actionReference;

		public EventSubscription(DelegateReference actionReference)
		{
			if (actionReference == null)
				throw new ArgumentNullException(nameof(actionReference));
			if (!(actionReference.Target is Action))
				throw new ArgumentException($"InvalidDelegateRerefenceType: {typeof(Action).FullName}", nameof(actionReference));

			_actionReference = actionReference;
		}
		public Action? Action => _actionReference.Target as Action;
		public SubscriptionToken? SubscriptionToken { get; set; }

		public virtual Action<object?[]>? GetExecutionStrategy()
		{
			if (Action is Action action)
				return arguments => InvokeAction(action);
			return null;
		}

		public virtual void InvokeAction(Action action)
		{
			if (action == null) throw new ArgumentNullException(nameof(action));
			action();
		}
	}

	/// <summary>
	/// イベントに対しての購読
	/// </summary>
	public class EventSubscription<TPayload> : IEventSubscription
	{
		private readonly DelegateReference _actionReference;
		private readonly DelegateReference _filterReference;

		public EventSubscription(DelegateReference actionReference, DelegateReference filterReference)
		{
			if (actionReference == null)
				throw new ArgumentNullException(nameof(actionReference));
			if (!(actionReference.Target is Action<TPayload>))
				throw new ArgumentException($"InvalidDelegateRerefenceType: {typeof(Action<TPayload>).FullName}", nameof(actionReference));

			if (filterReference == null)
				throw new ArgumentNullException(nameof(filterReference));
			if (!(filterReference.Target is Predicate<TPayload>))
				throw new ArgumentException($"InvalidDelegateRerefenceType: {typeof(Predicate<TPayload>).FullName}", nameof(filterReference));

			_actionReference = actionReference;
			_filterReference = filterReference;
		}

		public Action<TPayload?>? Action => _actionReference?.Target as Action<TPayload?>;
		public Predicate<TPayload?>? Filter => _filterReference?.Target as Predicate<TPayload?>;

		public SubscriptionToken? SubscriptionToken { get; set; }

		public virtual Action<object?[]>? GetExecutionStrategy()
		{
			if (Action is not Action<TPayload?> action || Filter is not Predicate<TPayload?> filter)
				return null;

			return arguments =>
			{
				TPayload argument = default;
				if (arguments != null && arguments.Length > 0 && arguments[0] != null)
					argument = (TPayload)arguments[0];
				if (filter(argument))
					InvokeAction(action, argument);
			};
		}
		public virtual void InvokeAction(Action<TPayload?> action, TPayload? argument)
		{
			if (action == null) throw new ArgumentNullException(nameof(action));
			action(argument);
		}
	}
}
