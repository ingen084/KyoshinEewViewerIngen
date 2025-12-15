using Avalonia.Controls;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace KyoshinEewViewer.Series.ShakeDetectionVerifier;

public partial class ShakeDetectionVerifierView : UserControl
{
	public ShakeDetectionVerifierView()
	{
		InitializeComponent();

		// テーマ変更時にMapControlのリソースキャッシュを更新
		KyoshinEewViewerApp.Selector?.WhenAnyValue(x => x.SelectedWindowTheme)
			.Where(x => x != null)
			.Subscribe(x =>
			{
				LeftMapControl.RefreshResourceCache(x!.Theme);
				RightMapControl.RefreshResourceCache(x!.Theme);
			});
	}
}
