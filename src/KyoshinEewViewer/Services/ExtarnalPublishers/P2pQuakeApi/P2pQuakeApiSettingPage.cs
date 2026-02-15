using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Series;
using Splat;

namespace KyoshinEewViewer.Services.ExtarnalPublishers.P2pQuakeApi;

public class P2pQuakeApiSettingPage : ISettingPage
{
	public bool IsVisible => true;

	public string? Icon => null;

	public string Title => "P2P地震情報 API";

	public Control DisplayControl => new P2pQuakeApiPage() { DataContext = this };

	public ISettingPage[] SubPages => [];

	public KyoshinEewViewerConfiguration Config { get; }
	public P2pQuakeApiInformationProvider Client { get; }

	public P2pQuakeApiSettingPage(KyoshinEewViewerConfiguration config, P2pQuakeApiInformationProvider client)
	{
		SplatRegistrations.RegisterLazySingleton<P2pQuakeApiSettingPage>();

		Config = config;
		Client = client;
	}
}
