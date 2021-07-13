using DmdataSharp.ApiResponses.V1;

namespace KyoshinEewViewer.Core.Models.Events
{
	public class DmdataBillingInfoUpdated
	{
		public DmdataBillingInfoUpdated(BillingResponse billingInfo)
		{
			BillingInfo = billingInfo;
		}

		public BillingResponse BillingInfo { get; }
	}
}
