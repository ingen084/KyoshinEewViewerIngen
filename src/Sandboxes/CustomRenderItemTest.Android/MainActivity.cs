using Android.App;
using Android.Content.PM;
using Avalonia;
using Avalonia.Android;
using Avalonia.ReactiveUI;

namespace CustomRenderItemTest.Android;

[Activity(Label = "CustomRenderItemTest.Android",
	Theme = "@style/MyTheme.NoActionBar",
	Icon = "@drawable/icon",
	MainLauncher = true,
	LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public class MainActivity : AvaloniaMainActivity<App>
{
	protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
	{
		return base.CustomizeAppBuilder(builder)
			.UseSkia()
			.UseReactiveUI();
	}
}
