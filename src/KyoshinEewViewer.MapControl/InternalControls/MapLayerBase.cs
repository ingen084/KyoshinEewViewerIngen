using KyoshinEewViewer.MapControl.Projections;
using KyoshinMonitorLib;
using System.Windows;

namespace KyoshinEewViewer.MapControl.InternalControls
{
	internal abstract class MapLayerBase : FrameworkElement
	{
		public MapProjection Projection { get; set; }
		public double Zoom { get; set; }
		public Location CenterLocation { get; set; }
	}
}
