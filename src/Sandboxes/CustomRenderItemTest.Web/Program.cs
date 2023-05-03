using Avalonia;
using Avalonia.Browser;
using Avalonia.ReactiveUI;
using CustomRenderItemTest;
using System.Runtime.Versioning;
using System.Threading.Tasks;

[assembly: SupportedOSPlatform("browser")]

internal class Program
{
	private static async Task Main(string[] args) => await BuildAvaloniaApp()
		.UseReactiveUI()
		.UseSkia()
		.SetupBrowserAppAsync();

	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>();
}
