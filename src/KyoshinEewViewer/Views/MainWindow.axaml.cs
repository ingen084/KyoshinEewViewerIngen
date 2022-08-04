using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using ReactiveUI;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Views;

public class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
#if DEBUG
		this.AttachDevTools();
#endif
	}

	private bool IsFullScreen { get; set; }

	private Dictionary<IPointer, Point> StartPoints { get; } = new();

	double GetLength(Point p)
		=> Math.Sqrt(p.X * p.X + p.Y * p.Y);

	private void InitializeComponent()
	{
		AvaloniaXamlLoader.Load(this);

		WindowState = ConfigurationService.Current.WindowState;
		if (ConfigurationService.Current.WindowLocation is Core.Models.KyoshinEewViewerConfiguration.Point2D position && position.X != -32000 && position.Y != -32000)
		{
			Position = new PixelPoint((int)position.X, (int)position.Y);
			WindowStartupLocation = WindowStartupLocation.Manual;
		}
		if (ConfigurationService.Current.WindowSize is Core.Models.KyoshinEewViewerConfiguration.Point2D size)
			ClientSize = new Size(size.X, size.Y);

		// フルスクリーンモード
		KeyDown += (s, e) =>
		{
			if (e.Key != Key.F11)
				return;

			if (IsFullScreen)
			{
				WindowState = WindowState.Normal;
				IsFullScreen = false;
				return;
			}
			WindowState = WindowState.FullScreen;
			IsFullScreen = true;
		};

		// �}�b�v�\���I�v�V�����ɂ��{�^���̕\���R���g���[��
		ConfigurationService.Current.Map.WhenAnyValue(x => x.DisableManualMapControl).Subscribe(x =>
		{
			this.FindControl<Button>("homeButton")!.IsVisible = !x;
			this.FindControl<Button>("homeButton2")!.IsVisible = !x;
		});
		this.FindControl<Button>("homeButton")!.IsVisible = !ConfigurationService.Current.Map.DisableManualMapControl;
		this.FindControl<Button>("homeButton2")!.IsVisible = !ConfigurationService.Current.Map.DisableManualMapControl;

		// �}�b�v�܂��̃n���h��
		map = this.FindControl<MapControl>("map")!;
		App.Selector?.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
				.Subscribe(x => map.RefreshResourceCache());
		var mapHitbox = this.FindControl<Grid>("mapHitbox")!;
		KyoshinMonitorLib.Location GetLocation(Point p)
		{
			var centerPix = map!.CenterLocation.ToPixel(map.Zoom);
			var originPix = new PointD(centerPix.X + ((map.PaddedRect.Width / 2) - p.X) + map.PaddedRect.Left, centerPix.Y + ((map.PaddedRect.Height / 2) - p.Y) + map.PaddedRect.Top);
			return originPix.ToLocation(map.Zoom);
		}
		mapHitbox.PointerPressed += (s, e) =>
		{
			var originPos = e.GetCurrentPoint(mapHitbox).Position / ConfigurationService.Current.WindowScale;
			StartPoints.Add(e.Pointer, originPos);
			// 3点以上の場合は2点になるようにする
			if (StartPoints.Count > 2)
				foreach (var pointer in StartPoints.Where(p => p.Key != e.Pointer).Select(p => p.Key).ToArray())
				{
					if (StartPoints.Count <= 2)
						break;
					StartPoints.Remove(pointer);
				}
		};
		mapHitbox.PointerMoved += (s, e) =>
		{
			if (!StartPoints.ContainsKey(e.Pointer))
				return;
			var newPosition = e.GetCurrentPoint(mapHitbox).Position / ConfigurationService.Current.WindowScale;
			var beforePoint = StartPoints[e.Pointer];
			var vector = beforePoint - newPosition;
			if (vector.IsDefault)
				return;
			StartPoints[e.Pointer] = newPosition;

			if (ConfigurationService.Current.Map.DisableManualMapControl || map.IsNavigating)
				return;

			if (StartPoints.Count <= 1)
				map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) + (PointD)vector).ToLocation(map.Zoom);
			else
			{
				var lockPos = StartPoints.First(p => p.Key != e.Pointer).Value;

				var befLen = GetLength(lockPos - beforePoint);
				var newLen = GetLength(lockPos - newPosition);
				var lockLoc = GetLocation(lockPos);

				var df = (befLen > newLen ? -1 : 1) * GetLength(vector) * .01;
				if (Math.Abs(df) < .02)
				{
					map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) + (PointD)vector).ToLocation(map.Zoom);
					return;
				}
				map.Zoom += df;

				var newCenterPix = map.CenterLocation.ToPixel(map.Zoom);
				var goalOriginPix = lockLoc.ToPixel(map.Zoom);

				var paddedRect = map.PaddedRect;
				var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - lockPos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - lockPos.Y) + paddedRect.Top);
				map.CenterLocation = (newCenterPix - (goalOriginPix - newMousePix)).ToLocation(map.Zoom);
			}
		};
		mapHitbox.PointerReleased += (s, e) => StartPoints.Remove(e.Pointer);
		mapHitbox.PointerWheelChanged += (s, e) =>
		{
			if (ConfigurationService.Current.Map.DisableManualMapControl || map.IsNavigating)
				return;

			var mousePos = e.GetCurrentPoint(mapHitbox).Position / ConfigurationService.Current.WindowScale;
			var mouseLoc = GetLocation(mousePos);

			var newZoom = Math.Clamp(map.Zoom + e.Delta.Y * 0.25, map.MinZoom, map.MaxZoom);

			var newCenterPix = map.CenterLocation.ToPixel(newZoom);
			var goalMousePix = mouseLoc.ToPixel(newZoom);

			var paddedRect = map.PaddedRect;
			var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			map.Zoom = newZoom;
			map.CenterLocation = (newCenterPix - (goalMousePix - newMousePix)).ToLocation(newZoom);
		};

		map.Zoom = 6;
		map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

		this.FindControl<Button>("settingsButton")!.Click += (s, e) => SubWindowsService.Default.ShowSettingWindow();
		this.FindControl<Button>("settingsButton2")!.Click += (s, e) => SubWindowsService.Default.ShowSettingWindow();
		this.FindControl<Button>("updateButton")!.Click += (s, e) => SubWindowsService.Default.ShowUpdateWindow();
		this.FindControl<Button>("updateButton2")!.Click += (s, e) => SubWindowsService.Default.ShowUpdateWindow();

		// LayoutTransform�̃o�O�΍�̂��߃X�P�[���ω����ɂ�Padding��}�������邽�߂Ƀ��C�A�E�g������
		this.WhenAnyValue(x => x.DataContext)
			.Subscribe(c => (c as MainWindowViewModel)?.WhenAnyValue(x => x.Scale).Subscribe(s => InvalidateMeasure()));
		// �T�C�Y�ύX���Ƀ��C�A�E�g������
		this.WhenAnyValue(x => x.Bounds).Subscribe(x => InvalidateMeasure());

		MessageBus.Current.Listen<Core.Models.Events.MapNavigationRequested>().Subscribe(x =>
		{
			if (!ConfigurationService.Current.Map.AutoFocus)
				return;
			if (x.Bound is Rect rect)
			{
				if (x.MustBound is Rect mustBound)
					map.Navigate(rect, ConfigurationService.Current.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero, mustBound);
				else
					map.Navigate(rect, ConfigurationService.Current.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero);
			}
			else
				NavigateToHome();
		});
		MessageBus.Current.Listen<Core.Models.Events.RegistMapPositionRequested>().Subscribe(x =>
		{
			// �n�����W�ɍ��킹�邽�ߏ����������Ă���
			var halfPaddedRect = new PointD(map.PaddedRect.Width / 2, -map.PaddedRect.Height / 2);
			var centerPixel = map.CenterLocation.ToPixel(map.Zoom);

			ConfigurationService.Current.Map.Location1 = (centerPixel + halfPaddedRect).ToLocation(map.Zoom);
			ConfigurationService.Current.Map.Location2 = (centerPixel - halfPaddedRect).ToLocation(map.Zoom);
		});
		MessageBus.Current.Listen<Core.Models.Events.ShowSettingWindowRequested>().Subscribe(x => SubWindowsService.Default.ShowSettingWindow());
		MessageBus.Current.Listen<Core.Models.Events.ShowMainWindowRequested>().Subscribe(x =>
		{
			Topmost = true;
			Show();
			Topmost = false;
		});

		Task.Run(async () =>
		{
			await Task.Delay(1000);
			NavigateToHome();
		});
	}

	private void NavigateToHome()
		=> map?.Navigate(
			new RectD(ConfigurationService.Current.Map.Location1.CastPoint(), ConfigurationService.Current.Map.Location2.CastPoint()),
			ConfigurationService.Current.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero);

	protected override void OnMeasureInvalidated()
	{
		if (DataContext is MainWindowViewModel vm)
		{
			var grid = this.FindControl<Grid>("mainGrid")!;
			var desiredSize = new Size(DesiredSize.Width, DesiredSize.Height - Padding.Top - Padding.Bottom);
			var origSize = desiredSize * vm.Scale;
			var size = (origSize - desiredSize) / vm.Scale;
			grid.Margin = new Thickness(size.Width / 2, size.Height / 2, size.Width / 2, size.Height / 2);
		}
		base.OnMeasureInvalidated();
	}

	private MapControl? map;
	private bool IsHideAnnounced { get; set; }

	protected override void HandleWindowStateChanged(WindowState state)
	{
		if (state == WindowState.Minimized && ConfigurationService.Current.Notification.HideWhenMinimizeWindow && (Locator.Current.GetService<NotificationService>()?.TrayIconAvailable ?? false))
		{
			Hide();
			if (!IsHideAnnounced)
			{
				Locator.Current.GetService<NotificationService>()?.Notify("タスクトレイに格納しました", "アプリケーションは実行中です");
				IsHideAnnounced = true;
			}
			return;
		}
		base.HandleWindowStateChanged(state);
	}

	public new void Close()
	{
		SaveConfig();
		base.Close();
	}


	protected override bool HandleClosing()
	{
		if (ConfigurationService.Current.Notification.HideWhenClosingWindow && (Locator.Current.GetService<NotificationService>()?.TrayIconAvailable ?? false))
		{
			Hide();
			if (!IsHideAnnounced)
			{
				Locator.Current.GetService<NotificationService>()?.Notify("タスクトレイに格納しました", "アプリケーションは実行中です");
				IsHideAnnounced = true;
			}
			return true;
		}
		SaveConfig();
		return base.HandleClosing();
	}

	private void SaveConfig()
	{
		ConfigurationService.Current.WindowState = WindowState;
		if (WindowState != WindowState.Minimized)
		{
			ConfigurationService.Current.WindowLocation = new(Position.X, Position.Y);
			if (WindowState != WindowState.Maximized)
				ConfigurationService.Current.WindowSize = new(ClientSize.Width, ClientSize.Height);
		}
		if (DataContext is MainWindowViewModel vm && !StartupOptions.IsStandalone)
			ConfigurationService.Current.SelectedTabName = vm.SelectedSeries?.Name;
		ConfigurationService.Save();
	}
}
