using System.Runtime.Versioning;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Browser;
using Avalonia.ReactiveUI;

using CustomRenderItemTest;
using KyoshinEewViewer.Map.Data;

[assembly: SupportedOSPlatform("browser")]

internal partial class Program
{
	private static async Task Main(string[] args)
	{
		PolygonFeature.AsyncVerticeMode = false;
		await BuildAvaloniaApp()
			.UseReactiveUI()
			.StartBrowserAppAsync("out");
	}

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
