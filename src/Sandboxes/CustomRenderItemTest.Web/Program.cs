using Avalonia;
using Avalonia.Browser;
using Avalonia.ReactiveUI;
using CustomRenderItemTest;
using System.Runtime.Versioning;

[assembly: SupportedOSPlatform("browser")]

internal partial class Program
{
	private static void Main(string[] args) => BuildAvaloniaApp()
		.UseReactiveUI()
		.UseSkia()
		.SetupBrowserApp("out");

	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>();
}
