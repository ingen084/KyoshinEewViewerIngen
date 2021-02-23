using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Core.Events
{
	/// <summary>
	/// publish/subscribe型のモデルを定義する
	/// </summary>
	public class PubSubEvent : EventBase
	{
		public SubscriptionToken Subscribe(Action action)
			=> Subscribe(action, ThreadOption.PublisherThread);
		public SubscriptionToken Subscribe(Action action, ThreadOption threadOption)
			=> Subscribe(action, threadOption, false);

		public SubscriptionToken Subscribe(Action action, bool keepSubscriberReferenceAlive)
			=> Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive);

		public virtual SubscriptionToken Subscribe(Action action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
		{
			var actionReference = new DelegateReference(action, keepSubscriberReferenceAlive);

			EventSubscription subscription;
			switch (threadOption)
			{
				case ThreadOption.BackgroundThread:
					subscription = new BackgroundEventSubscription(actionReference);
					break;
				case ThreadOption.UIThread:
					if (SynchronizationContext == null) throw new InvalidOperationException("EventAggregatorNotConstructedOnUIThread");
					subscription = new DispatcherEventSubscription(actionReference, SynchronizationContext);
					break;
				case ThreadOption.PublisherThread:
				default:
					subscription = new EventSubscription(actionReference);
					break;
			}

			return InternalSubscribe(subscription);
		}

		public virtual void Publish() => InternalPublish();
		public virtual void Unsubscribe(Action subscriber)
		{
			lock (Subscriptions)
				if (Subscriptions.Cast<EventSubscription>().FirstOrDefault(evt => evt.Action == subscriber) is IEventSubscription eventSubscription)
					Subscriptions.Remove(eventSubscription);
		}
		public virtual bool Contains(Action subscriber)
		{
			IEventSubscription? eventSubscription;
			lock (Subscriptions)
				eventSubscription = Subscriptions.Cast<EventSubscription>().FirstOrDefault(evt => evt.Action == subscriber);
			return eventSubscription != null;
		}
	}
	/// <summary>
	/// publish/subscribe型のモデルを定義する
	/// </summary>
	public class PubSubEvent<TPayload> : EventBase
	{
		public SubscriptionToken Subscribe(Action<TPayload> action) => Subscribe(action, ThreadOption.PublisherThread);
		public virtual SubscriptionToken Subscribe(Action<TPayload> action, Predicate<TPayload> filter)
			=> Subscribe(action, ThreadOption.PublisherThread, false, filter);
		public SubscriptionToken Subscribe(Action<TPayload> action, ThreadOption threadOption)
			=> Subscribe(action, threadOption, false);
		public SubscriptionToken Subscribe(Action<TPayload> action, bool keepSubscriberReferenceAlive)
			=> Subscribe(action, ThreadOption.PublisherThread, keepSubscriberReferenceAlive);
		public SubscriptionToken Subscribe(Action<TPayload> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive)
			=> Subscribe(action, threadOption, keepSubscriberReferenceAlive, null);

		public virtual SubscriptionToken Subscribe(Action<TPayload> action, ThreadOption threadOption, bool keepSubscriberReferenceAlive, Predicate<TPayload>? filter)
		{
			var actionReference = new DelegateReference(action, keepSubscriberReferenceAlive);
			var filterReference = new DelegateReference(filter ?? new Predicate<TPayload>(delegate { return true; }), true);

			EventSubscription<TPayload> subscription;
			switch (threadOption)
			{
				case ThreadOption.PublisherThread:
					subscription = new EventSubscription<TPayload>(actionReference, filterReference);
					break;
				case ThreadOption.BackgroundThread:
					subscription = new BackgroundEventSubscription<TPayload>(actionReference, filterReference);
					break;
				case ThreadOption.UIThread:
					if (SynchronizationContext == null) throw new InvalidOperationException("EventAggregatorNotConstructedOnUIThread");
					subscription = new DispatcherEventSubscription<TPayload>(actionReference, filterReference, SynchronizationContext);
					break;
				default:
					subscription = new EventSubscription<TPayload>(actionReference, filterReference);
					break;
			}
			return InternalSubscribe(subscription);
		}

		public virtual void Publish(TPayload payload)
			=> InternalPublish(payload);
		public virtual void Unsubscribe(Action<TPayload> subscriber)
		{
			lock (Subscriptions)
				if (Subscriptions.Cast<EventSubscription<TPayload>>().FirstOrDefault(evt => evt.Action == subscriber) is IEventSubscription eventSubscription)
					Subscriptions.Remove(eventSubscription);
		}
		public virtual bool Contains(Action<TPayload> subscriber)
		{
			IEventSubscription? eventSubscription;
			lock (Subscriptions)
				eventSubscription = Subscriptions.Cast<EventSubscription<TPayload>>().FirstOrDefault(evt => evt.Action == subscriber);
			return eventSubscription != null;
		}
	}
}
