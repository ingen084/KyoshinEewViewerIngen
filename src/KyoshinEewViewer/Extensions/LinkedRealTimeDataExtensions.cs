using KyoshinMonitorLib;

namespace KyoshinEewViewer.Extensions
{
	public static class LinkedRealtimeDataExtensions
	{
		public static string GetPointIdentity(this LinkedRealtimeData data)
		{
			if (data.ObservationPoint.Point == null)
				return data.ObservationPoint.Site.SiteId;
			return data.ObservationPoint.Point.Code;
		}
	}
}