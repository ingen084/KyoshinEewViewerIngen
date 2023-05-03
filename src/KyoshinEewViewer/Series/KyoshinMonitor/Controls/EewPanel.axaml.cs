using Avalonia;
using Avalonia.Controls;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Controls;
public partial class EewPanel : UserControl
{
	public EewPanel()
	{
		InitializeComponent();
	}

	public static readonly DirectProperty<EewPanel, bool> ShowAccuracyProperty =
		AvaloniaProperty.RegisterDirect<EewPanel, bool>(
			nameof(ShowAccuracy),
			o => o.ShowAccuracy,
			(o, v) => o.ShowAccuracy = v
		);

	private bool _showAccuracy = true;

	public bool ShowAccuracy
	{
		get => _showAccuracy;
		set {
			if (!SetAndRaise(ShowAccuracyProperty, ref _showAccuracy, value))
				return;
			System.Diagnostics.Debug.WriteLine($"{GetHashCode()}: {value}");
			WarningAreaHead.IsVisible = value;
			WarningAreaBody.IsVisible = value;
		}
	}
}
