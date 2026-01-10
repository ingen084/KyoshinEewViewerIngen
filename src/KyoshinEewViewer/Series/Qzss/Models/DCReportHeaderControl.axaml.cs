using Avalonia;
using Avalonia.Controls;

namespace KyoshinEewViewer.Series.Qzss.Models;

public partial class DCReportHeaderControl : UserControl
{
	public static readonly StyledProperty<string> TitleProperty =
		AvaloniaProperty.Register<DCReportHeaderControl, string>(nameof(Title), "防災気象情報");

	public string Title
	{
		get => GetValue(TitleProperty);
		set => SetValue(TitleProperty, value);
	}

	public static readonly StyledProperty<bool> ShowReportCountProperty =
		AvaloniaProperty.Register<DCReportHeaderControl, bool>(nameof(ShowReportCount), false);

	public bool ShowReportCount
	{
		get => GetValue(ShowReportCountProperty);
		set => SetValue(ShowReportCountProperty, value);
	}

	public DCReportHeaderControl()
	{
		InitializeComponent();
	}
}
