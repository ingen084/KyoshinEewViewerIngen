using Avalonia.Controls;
using KyoshinEewViewer.CustomControl;
using KyoshinEewViewer.Map.Data;
using KyoshinEewViewer.Map.Layers;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CustomRenderItemTest.Views;
public partial class MainView : UserControl
{
	public MainView()
	{
		InitializeComponent();

		App.Selector?.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
				.Subscribe(x => Map.RefreshResourceCache());

		Map.Zoom = 6;
		Map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

		Task.Run(async () =>
		{
			var mapData = await MapData.LoadDefaultMapAsync();
			var landLayer = new LandLayer { Map = mapData };
			var landBorderLayer = new LandBorderLayer { Map = mapData };
			Map.Layers = [
				landLayer,
				landBorderLayer,
				new GridLayer(),
			];
		});
	}
}
