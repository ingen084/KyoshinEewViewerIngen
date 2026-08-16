using Avalonia.Controls;
using Avalonia.LogicalTree;
using R3;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace KyoshinEewViewer.Series.ShakeDetectionVerifier;

public partial class ShakeDetectionVerifierView : UserControl
{
	private IDisposable? _themeSubscription;

	public ShakeDetectionVerifierView()
	{
		InitializeComponent();
	}

	protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
	{
		base.OnAttachedToLogicalTree(e);

		// テーマ変更時にMapControlのリソースキャッシュを更新
		// この View はシリーズ切替のたびに作り直されるため、アプリ生存期間の Selector への購読はツリー所属期間に限定してリークを防ぐ
		_themeSubscription ??= KyoshinEewViewerApp.Selector?.ObservePropertyChanged(x => x.SelectedWindowTheme)
			.Where(x => x != null)
			.Subscribe(x =>
			{
				LeftMapControl.RefreshResourceCache(x!.Theme);
				RightMapControl.RefreshResourceCache(x!.Theme);
			});
	}

	protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
	{
		_themeSubscription?.Dispose();
		_themeSubscription = null;

		base.OnDetachedFromLogicalTree(e);
	}
}
