using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series;

namespace KyoshinEewViewer.Services.ExternalPublishers.Axis;

public class AxisSettingPage : ISettingPage
{
	public bool IsVisible => true;

	public string? Icon => null;

	public string Title => "AXIS(試験中)";

	public Control DisplayControl => new AxisPage() { DataContext = this };

	public ISettingPage[] SubPages => [];


	public KyoshinEewViewerConfiguration Config { get; }
	public AxisInformationProvider Client { get; }

	public AxisSettingPage(KyoshinEewViewerConfiguration config, AxisInformationProvider client)
	{
		Config = config;
		Client = client;
	}
}
