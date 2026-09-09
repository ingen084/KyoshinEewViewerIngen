using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using KyoshinEewViewer.Desktop.Platforms.MacOS;
using System;

namespace KyoshinEewViewer.Desktop.Views;

public partial class SplashWindow : Window
{
	private MacGlassSplash? _glass;

	public SplashWindow() : this(OperatingSystem.IsMacOSVersionAtLeast(26), true) { }

	// ガラスを利用できない場合は、他プラットフォームと同じXAMLの共通画像を使う。
	internal SplashWindow(bool useTahoeAppearance, bool useNativeGlass)
	{
		InitializeComponent();
		if (!useTahoeAppearance || !useNativeGlass || !OperatingSystem.IsMacOSVersionAtLeast(26))
			return;

		var originalBackground = Background;
		var originalTransparency = TransparencyLevelHint;
		Background = Brushes.Transparent;
		TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
		_glass = new MacGlassSplash(active =>
		{
			SplashImage.IsVisible = !active;
			Background = active ? Brushes.Transparent : originalBackground;
			TransparencyLevelHint = active ? [WindowTransparencyLevel.Transparent] : originalTransparency;
		});
		SplashPanel.Children.Add(_glass);
		ActualThemeVariantChanged += ThemeChanged;
		UpdateTheme();
	}

	private void ThemeChanged(object? sender, EventArgs e) => UpdateTheme();

	private void UpdateTheme()
	{
		if (OperatingSystem.IsMacOSVersionAtLeast(26))
			_glass?.SetDark(ActualThemeVariant == ThemeVariant.Dark);
	}

	protected override void OnClosed(EventArgs e)
	{
		ActualThemeVariantChanged -= ThemeChanged;
		base.OnClosed(e);
	}
}
