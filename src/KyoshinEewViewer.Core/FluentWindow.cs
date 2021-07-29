using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Platform;
using Avalonia.Styling;
using System;

namespace KyoshinEewViewer.Core
{
	public class FluentWindow : Window, IStyleable
	{
		Type IStyleable.StyleKey => typeof(Window);

		public FluentWindow()
		{
			ExtendClientAreaToDecorationsHint = true;
			ExtendClientAreaTitleBarHeightHint = -1;

			TransparencyLevelHint = WindowTransparencyLevel.AcrylicBlur;

			this.GetObservable(WindowStateProperty)
				.Subscribe(x =>
				{
					Padding = new Thickness(x == WindowState.Maximized ? 6 : 0);
					PseudoClasses.Set(":maximized", x == WindowState.Maximized);
					PseudoClasses.Set(":fullscreen", x == WindowState.FullScreen);
				});

			this.GetObservable(IsExtendedIntoWindowDecorationsProperty)
				.Subscribe(x =>
				{
					if (!x)
					{
						SystemDecorations = SystemDecorations.Full;
						TransparencyLevelHint = WindowTransparencyLevel.Blur;
					}
				});
		}

		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			ExtendClientAreaChromeHints =
				ExtendClientAreaChromeHints.PreferSystemChrome |
				ExtendClientAreaChromeHints.OSXThickTitleBar;
		}
	}
}
