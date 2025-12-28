using Avalonia;
using Avalonia.Markup.Xaml;
using KyoshinEewViewer;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using Splat;

namespace ShakeDetectionProducer;

public class App : Application
{
	public override void Initialize() => AvaloniaXamlLoader.Load(this);

	public override void OnFrameworkInitializationCompleted()
	{
		Utils.OverrideVersion = "ShakeDetectionProducer";
		KyoshinEewViewerApp.Application = this;

		KyoshinEewViewerApp.Selector = ThemeSelector.Create(".");
		KyoshinEewViewerApp.Selector.EnableThemes(this);

		base.OnFrameworkInitializationCompleted();
	}

	public override void RegisterServices()
	{
		Locator.CurrentMutable.RegisterLazySingleton(ConfigurationLoader.Load, typeof(KyoshinEewViewerConfiguration));
		var config = Locator.Current.RequireService<KyoshinEewViewerConfiguration>();

		// 強制設定
		config.Logging.Enable = true;
		config.Map.AutoFocusAnimation = false;
		config.Update.SendCrashReport = false;
		config.KyoshinMonitor.UseExperimentalShakeDetect = true;
		config.Earthquake.ShowHistory = false;

		LoggingAdapter.Setup(config);

		KyoshinEewViewerApp.SetupIOC(Locator.GetLocator());
		SplatRegistrations.SetupIOC(Locator.GetLocator());
		base.RegisterServices();
	}
}
