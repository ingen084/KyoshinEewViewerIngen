using Avalonia.Controls;
using KyoshinEewViewer.ViewModels;
using R3;
using System;

namespace KyoshinEewViewer.Views;

public partial class DebugWindow : Window
{
	private IDisposable? _scrollSubscription;

	public DebugWindow()
	{
		InitializeComponent();

		// DataContextが設定されたらViewModelを監視
		DataContextChanged += OnDataContextChanged;
	}

	private void OnDataContextChanged(object? sender, EventArgs e)
	{
		_scrollSubscription?.Dispose();

		if (DataContext is DebugWindowViewModel vm)
		{
			// ScrollToEndプロパティの変更を監視
			_scrollSubscription = vm.ObservePropertyChanged(x => x.ScrollToEnd)
				.Subscribe(shouldScroll =>
				{
					if (shouldScroll && vm.LogEntries.Count > 0)
					{
						// 最後の項目にスクロール
						LogDataGrid.ScrollIntoView(vm.LogEntries[^1], null);
					}
				});
		}
	}

	protected override void OnClosed(EventArgs e)
	{
		_scrollSubscription?.Dispose();
		DataContextChanged -= OnDataContextChanged;
		base.OnClosed(e);
	}
}
