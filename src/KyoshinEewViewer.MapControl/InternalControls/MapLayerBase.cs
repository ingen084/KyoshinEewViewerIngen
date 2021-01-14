using KyoshinMonitorLib;
using System.Windows;

namespace KyoshinEewViewer.MapControl.InternalControls
{
	internal abstract class MapLayerBase : FrameworkElement
	{
		public double Zoom { get; set; }
		public Location CenterLocation { get; set; }
	}
}
