using System.Runtime.Versioning;
using Avalonia;
using Avalonia.ReactiveUI;
using Avalonia.Browser;
using CustomRenderItemTest;

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
