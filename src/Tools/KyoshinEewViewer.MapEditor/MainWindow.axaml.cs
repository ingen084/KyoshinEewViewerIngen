using Avalonia.Controls;
using System.IO;

namespace KyoshinEewViewer.MapEditor;
public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		DataGrid.ItemsSource = KyoshinMonitorLib.ObservationPoint.LoadFromMpk(@"C:\Source\Repos\KyoshinEewViewerIngenNew\src\KyoshinEewViewer\Resources\ShindoObsPoints.mpk.lz4", true);
		// "../../../../../KyoshinEewViewer/Resources/ShindoObsPoints.mpk.lz4"

	}
}
