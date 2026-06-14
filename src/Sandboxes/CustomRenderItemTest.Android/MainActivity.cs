using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace CustomRenderItemTest.Android;

[Activity(Label = "CustomRenderItemTest.Android",
	Theme = "@style/MyTheme.NoActionBar",
	Icon = "@drawable/icon",
	MainLauncher = true,
	ScreenOrientation = ScreenOrientation.Landscape,
	LaunchMode = LaunchMode.SingleTop,
	ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public class MainActivity : AvaloniaMainActivity
{
}
