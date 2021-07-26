using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EarthquakeRenderTest.ViewModels;
using EarthquakeRenderTest.Views;

namespace EarthquakeRenderTest
{
	public class App : Application
	{
		public override void Initialize() => AvaloniaXamlLoader.Load(this);

		public override void OnFrameworkInitializationCompleted()
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				desktop.MainWindow = new MainWindow
				{
					DataContext = new MainWindowViewModel(),
				};
			}

			base.OnFrameworkInitializationCompleted();
		}

		/// <summary>
		/// override RegisterServices register custom service
		/// </summary>
		//public override void RegisterServices()
		//{
		//	AvaloniaLocator.CurrentMutable.Bind<IFontManagerImpl>().ToConstant(new CustomFontManagerImpl());
		//	base.RegisterServices();
		//}
	}
}
