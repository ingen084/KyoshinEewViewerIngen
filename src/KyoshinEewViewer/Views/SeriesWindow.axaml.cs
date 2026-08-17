using Avalonia.Controls;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.Events;
using KyoshinEewViewer.Map;
using CommunityToolkit.Mvvm.Messaging;
using KyoshinEewViewer.ViewModels;
using R3;
using System;
using System.Reactive.Linq;

namespace KyoshinEewViewer.Views;

public partial class SeriesWindow : Window
{
	private IDisposable? _seriesNavigationSubscription;
	private IDisposable? _themeSubscription;
	private IDisposable? _manualMapControlSubscription;

	public SeriesWindow()
	{
		InitializeComponent();

		_themeSubscription = KyoshinEewViewerApp.Selector?.ObservePropertyChanged(x => x.SelectedWindowTheme)
			.Where(x => x != null)
			.Subscribe(x => Map.RefreshResourceCache(x!.Theme));

		var config = ServiceLocator.Current.RequireService<KyoshinEewViewerConfiguration>();
		_manualMapControlSubscription = config.Map.ObservePropertyChanged(x => x.DisableManualMapControl).Subscribe(x =>
		{
			HomeButton.IsVisible = !x;
			Map.IsDisableManualControl = x;
		});
		HomeButton.IsVisible = !config.Map.DisableManualMapControl;
		Map.IsDisableManualControl = config.Map.DisableManualMapControl;

		Map.Zoom = 6;
		Map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

		DataContextChanged += OnDataContextChanged;
		Opened += OnOpened;
	}

	private void OnOpened(object? sender, EventArgs e)
	{
		// ウィンドウが開ききってからSeriesの現在のナビゲーションポイントに移動
		if (DataContext is SeriesWindowViewModel vm)
		{
			var config = ServiceLocator.Current.RequireService<KyoshinEewViewerConfiguration>();
			NavigateMap(vm.Series.MapNavigationRequest, config);
		}
	}

	private void OnDataContextChanged(object? sender, EventArgs e)
	{
		StrongReferenceMessenger.Default.Unregister<SeriesWindowMapNavigationRequest>(this);
		_seriesNavigationSubscription?.Dispose();

		if (DataContext is SeriesWindowViewModel vm)
		{
			var config = ServiceLocator.Current.RequireService<KyoshinEewViewerConfiguration>();

			StrongReferenceMessenger.Default.Register<SeriesWindowMapNavigationRequest>(this, (_, x) =>
			{
				if (x.Series != vm.Series)
					return;
				NavigateMap(x.Request, config);
			});

			_seriesNavigationSubscription = vm.Series.ObservePropertyChanged(x => x.MapNavigationRequest)
				.Subscribe(x =>
				{
					if (config.Map.AutoFocus)
						NavigateMap(x, config);
				});
		}
	}

	private void NavigateMap(MapNavigationRequest? request, KyoshinEewViewerConfiguration config)
	{
		if (request?.Bound is { } rect)
		{
			if (request.MustBound is { } mustBound)
				Map.Navigate(rect, config.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero, mustBound);
			else
				Map.Navigate(rect, config.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero);
		}
		else
			NavigateToHome(config);
	}

	private void NavigateToHome(KyoshinEewViewerConfiguration config)
	{
		Map?.Navigate(
			new RectD(config.Map.Location1.CastPoint(), config.Map.Location2.CastPoint()),
			config.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero);
	}

	protected override void OnClosed(EventArgs e)
	{
		StrongReferenceMessenger.Default.Unregister<SeriesWindowMapNavigationRequest>(this);
		_seriesNavigationSubscription?.Dispose();
		_themeSubscription?.Dispose();
		_manualMapControlSubscription?.Dispose();

		base.OnClosed(e);
	}
}
