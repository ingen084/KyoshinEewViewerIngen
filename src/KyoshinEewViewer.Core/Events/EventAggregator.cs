using System;
using System.Collections.Generic;
using System.Threading;

namespace KyoshinEewViewer.Core.Events
{
	public class EventAggregator
	{
		public static EventAggregator Default { get; } = new();

		private readonly Dictionary<Type, EventBase> events = new Dictionary<Type, EventBase>();
		// Captures the sync context for the UI thread when constructed on the UI thread 
		// in a platform agnositc way so it can be used for UI thread dispatching
		private readonly SynchronizationContext syncContext = SynchronizationContext.Current ?? throw new InvalidOperationException("メインスレッドのSynchronizationContextを取得できませんでした");

		public TEventType GetEvent<TEventType>() where TEventType : EventBase, new()
		{
			lock (events)
			{
				if (events.TryGetValue(typeof(TEventType), out var existingEvent))
					return (TEventType)existingEvent;
				TEventType newEvent = new TEventType
				{
					SynchronizationContext = syncContext
				};
				events[typeof(TEventType)] = newEvent;

				return newEvent;
			}
		}
	}
}
