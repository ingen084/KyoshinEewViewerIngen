using Avalonia.Controls;
using System;

namespace KyoshinEewViewer.Series.Earthquake;

public partial class EarthquakeView : UserControl
{
	public EarthquakeView()
	{
		InitializeComponent();
	}

	public TopLevel GetTopLevel() => this.VisualRoot as TopLevel ?? throw new NullReferenceException("Invalid Owner");
}
