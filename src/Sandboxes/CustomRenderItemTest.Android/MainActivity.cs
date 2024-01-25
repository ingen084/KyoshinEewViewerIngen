using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Avalonia.ReactiveUI;
using KyoshinEewViewer.Core;

namespace CustomRenderItemTest.Android;

[Activity(Label = "CustomRenderItemTest.Android",
	Theme = "@style/MyTheme.NoActionBar",
	Icon = "@drawable/icon",
	MainLauncher = true,
	ScreenOrientation = ScreenOrientation.Landscape,
	LaunchMode = LaunchMode.SingleTop,
	ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public class MainActivity : AvaloniaMainActivity<App>
{
	protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
	{
		return base.CustomizeAppBuilder(builder)
			.UseKeviFonts()
			.UseReactiveUI();
	}
}
