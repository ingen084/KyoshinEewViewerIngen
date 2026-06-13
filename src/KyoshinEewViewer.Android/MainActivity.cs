using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace KyoshinEewViewer.Android;

[Activity(
    Label = "KyoshinEewViewer.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
	ScreenOrientation = ScreenOrientation.Landscape,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
