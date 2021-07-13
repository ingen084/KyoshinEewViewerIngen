using KyoshinMonitorLib;

namespace KyoshinEewViewer.Map.Projections
{
	public class MillerProjection : MercatorProjection
	{
		public float Rate { get; set; } = .8f;

		internal override PointD LatLngToPoint(Location location)
		{
			var result = base.LatLngToPoint(new Location(location.Latitude * Rate, location.Longitude));
			return new PointD(result.X, result.Y / Rate);
		}

		internal override Location PointToLatLng(PointD point)
		{
			var result = base.PointToLatLng(new PointD(point.X, point.Y * Rate));
			return new Location(result.Latitude / Rate, result.Longitude);
		}
	}
}
