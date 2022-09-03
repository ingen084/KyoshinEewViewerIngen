using Avalonia;
using Avalonia.Controls;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Controls;
public partial class EewPanel : UserControl
{
	public EewPanel()
	{
		InitializeComponent();
	}

	public static readonly StyledProperty<bool> ShowAccuracyProperty =
	AvaloniaProperty.Register<EewPanel, bool>(nameof(ShowAccuracy), notifying: (o, v) =>
	{
		if (o is EewPanel panel) {
			panel.warningAreaHead.IsVisible = v;
			panel.warningAreaBody.IsVisible = v;
		}
	});

	public bool ShowAccuracy
	{
		get => GetValue(ShowAccuracyProperty);
		set => SetValue(ShowAccuracyProperty, value);
	}
}
