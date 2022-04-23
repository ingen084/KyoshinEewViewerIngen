using KyoshinMonitorLib;
using System;

namespace KyoshinEewViewer.Series.KyoshinMonitor;

internal static class Extensions
{
	public static double Distance(this Location point1, Location point2)
		=> 6371 * Math.Acos(Math.Cos(point1.Latitude * Math.PI / 180) * Math.Cos(point2.Latitude * Math.PI / 180) * Math.Cos(point2.Longitude * Math.PI / 180 - point1.Longitude * Math.PI / 180) + Math.Sin(point1.Latitude * Math.PI / 180) * Math.Sin(point2.Latitude * Math.PI / 180));
}
