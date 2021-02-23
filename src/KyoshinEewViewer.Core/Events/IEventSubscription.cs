using System;

namespace KyoshinEewViewer.Core.Events
{
	public interface IEventSubscription
	{
		SubscriptionToken? SubscriptionToken { get; set; }
		Action<object?[]>? GetExecutionStrategy();
	}
}
