using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KyoshinEewViewer.Core.Events
{
	public abstract class EventBase
	{
		private readonly List<IEventSubscription> _subscriptions = new();

		public SynchronizationContext? SynchronizationContext { get; set; }
		protected ICollection<IEventSubscription> Subscriptions => _subscriptions;

		protected virtual SubscriptionToken InternalSubscribe(IEventSubscription eventSubscription)
		{
			if (eventSubscription == null) throw new ArgumentNullException(nameof(eventSubscription));

			eventSubscription.SubscriptionToken = new SubscriptionToken(Unsubscribe);

			lock (Subscriptions)
				Subscriptions.Add(eventSubscription);
			return eventSubscription.SubscriptionToken;
		}

		protected virtual void InternalPublish(params object?[] arguments)
		{
			List<Action<object?[]>> executionStrategies = PruneAndReturnStrategies();
			foreach (var executionStrategy in executionStrategies)
			{
				executionStrategy(arguments);
			}
		}

		public virtual void Unsubscribe(SubscriptionToken token)
		{
			lock (Subscriptions)
				if (Subscriptions.FirstOrDefault(evt => evt.SubscriptionToken == token) is EventSubscription subscription)
					Subscriptions.Remove(subscription);
		}

		public virtual bool Contains(SubscriptionToken token)
		{
			lock (Subscriptions)
				return Subscriptions.Any(evt => evt.SubscriptionToken == token);
		}

		private List<Action<object?[]>> PruneAndReturnStrategies()
		{
			var returnList = new List<Action<object?[]>>();

			lock (Subscriptions)
			{
				for (var i = Subscriptions.Count - 1; i >= 0; i--)
				{
					var listItem = _subscriptions[i].GetExecutionStrategy();

					if (listItem == null)
						// Prune from main list. Log?
						_subscriptions.RemoveAt(i);
					else
						returnList.Add(listItem);
				}
			}

			return returnList;
		}

		public void Prune()
		{
			lock (Subscriptions)
				for (var i = Subscriptions.Count - 1; i >= 0; i--)
					if (_subscriptions[i].GetExecutionStrategy() == null)
						_subscriptions.RemoveAt(i);
		}
	}
}
