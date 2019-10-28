using KyoshinMonitorLib;

namespace KyoshinEewViewer.Extensions
{
	public static class LinkedRealTimeDataExtensions
	{
		public static string GetPointHash(this LinkedRealTimeData data)
		{
			if (data.ObservationPoint.Point == null)
				return data.ObservationPoint.Site.SiteId;
			return data.ObservationPoint.Point.Code;
		}
	}
}