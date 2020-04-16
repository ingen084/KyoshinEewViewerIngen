using KyoshinMonitorLib;
using KyoshinMonitorLib.ApiResult.AppApi;

namespace KyoshinEewViewer.CustomControls
{
	public static class Extensions
	{
		public static string GetRegionName(this LinkedObservationPoint point)
		{
			if (point.Point == null)
			{
				if (point.Site == null)
					return "不明";
				else
					return point.Site.Prefefecture.GetLongName();
			}
			if (point.Point.Region.Contains(" "))
				return point.Point.Region[..point.Point.Region.IndexOf(' ')];
			return $"{point.Point.Region}";
		}
	}
}
