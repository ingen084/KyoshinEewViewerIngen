using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CustomRenderItemTest.ViewModels;
using CustomRenderItemTest.Views;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.CustomControl;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive.Linq;

namespace CustomRenderItemTest
{
	public class App : Application
	{
		public static ThemeSelector? Selector;

		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public override void OnFrameworkInitializationCompleted()
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				Selector = ThemeSelector.Create(".");
				Selector.EnableThemes(this);
				desktop.MainWindow = new MainWindow
				{
					DataContext = new MainWindowViewModel(),
				};
				Selector.WhenAnyValue(x => x.SelectedIntensityTheme).Where(x => x != null)
					.Subscribe(x => FixedObjectRenderer.UpdateIntensityPaintCache(desktop.MainWindow));
				desktop.Exit += (s, e) => MessageBus.Current.SendMessage(new ApplicationClosing());
			}
			base.OnFrameworkInitializationCompleted();
		}
	}
}
