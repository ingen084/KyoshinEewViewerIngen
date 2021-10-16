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
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Views
{
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

		private void InitializeComponent()
		{
			AvaloniaXamlLoader.Load(this);

			WindowState = ConfigurationService.Current.WindowState;
			if (ConfigurationService.Current.WindowLocation is Core.Models.KyoshinEewViewerConfiguration.Point2D position)
			{
				Position = new PixelPoint((int)position.X, (int)position.Y);
				WindowStartupLocation = WindowStartupLocation.Manual;
			}
			if (ConfigurationService.Current.WindowSize is Core.Models.KyoshinEewViewerConfiguration.Point2D size)
				ClientSize = new Size(size.X, size.Y);

			// �t���X�N���@�\
			KeyDown += (s, e) =>
			{
				if (e.Key != Key.F11)
					return;

				if (IsFullScreen)
				{
					SystemDecorations = SystemDecorations.Full;
					//if (IsExtendedIntoWindowDecorations)
					//	this.FindControl<Grid>("titleBar").IsVisible = true;
					WindowState = WindowState.Normal;
					IsFullScreen = false;
					return;
				}
				// ���łɍő剻����Ă���ꍇ���܂��t���X�N�ɂȂ�Ȃ��̂ň�U�ʏ��Ԃɖ߂�
				WindowState = WindowState.Normal;
				Dispatcher.UIThread.InvokeAsync(() =>
				{
					SystemDecorations = SystemDecorations.None;
					//this.FindControl<Grid>("titleBar").IsVisible = false;
					WindowState = WindowState.Maximized;
					IsFullScreen = true;
				});
			};

			// �}�b�v�\���I�v�V�����ɂ��{�^���̕\���R���g���[��
			ConfigurationService.Current.Map.WhenAnyValue(x => x.DisableManualMapControl).Subscribe(x => this.FindControl<Button>("homeButton").IsVisible = !x);
			this.FindControl<Button>("homeButton").IsVisible = !ConfigurationService.Current.Map.DisableManualMapControl;

			// �}�b�v�܂��̃n���h��
			map = this.FindControl<MapControl>("map");
			App.Selector?.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
					.Subscribe(x => map.RefreshResourceCache());
			var mapHitbox = this.FindControl<Grid>("mapHitbox");
			mapHitbox.PointerMoved += (s, e2) =>
			{
				var pointer = e2.GetCurrentPoint(this);
				var curPos = pointer.Position / ConfigurationService.Current.WindowScale;
				if (!ConfigurationService.Current.Map.DisableManualMapControl && pointer.Properties.IsLeftButtonPressed && !map.IsNavigating)
				{
					var diff = new PointD(_prevPos.X - curPos.X, _prevPos.Y - curPos.Y);
					map.CenterLocation = (map.CenterLocation.ToPixel(map.Projection, map.Zoom) + diff).ToLocation(map.Projection, map.Zoom);
				}

				_prevPos = curPos;
			};
			mapHitbox.PointerPressed += (s, e2) =>
			{
				var pointer = e2.GetCurrentPoint(this);
				if (pointer.Properties.IsLeftButtonPressed)
					_prevPos = pointer.Position / ConfigurationService.Current.WindowScale;
			};
			mapHitbox.PointerWheelChanged += (s, e) =>
			{
				if (ConfigurationService.Current.Map.DisableManualMapControl || map.IsNavigating)
					return;

				var pointer = e.GetCurrentPoint(this);
				var paddedRect = map.PaddedRect;
				var centerPix = map.CenterLocation.ToPixel(map.Projection, map.Zoom);
				var mousePos = pointer.Position / ConfigurationService.Current.WindowScale;
				var mousePix = new PointD(centerPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, centerPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);
				var mouseLoc = mousePix.ToLocation(map.Projection, map.Zoom);

				var length = Math.Sqrt(e.Delta.Y * e.Delta.Y + e.Delta.X * e.Delta.X);
				var newZoom = Math.Clamp(map.Zoom + e.Delta.Y * 0.25, map.MinZoom, map.MaxZoom);

				var newCenterPix = map.CenterLocation.ToPixel(map.Projection, newZoom);
				var goalMousePix = mouseLoc.ToPixel(map.Projection, newZoom);

				var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

				map.Zoom = newZoom;
				map.CenterLocation = (map.CenterLocation.ToPixel(map.Projection, newZoom) - (goalMousePix - newMousePix)).ToLocation(map.Projection, newZoom);

				//var paddedRectHalf = map.PaddedRect.Size / 2;
				//var newCenterPixel = map.CenterLocation.ToPixel(map.Projection, newZoom) - (goalMousePix - newMousePix);
				//map.Navigate(new RectD((newCenterPix - paddedRectHalf).ToLocation(map.Projection, newZoom).CastPoint(),
				//	(newCenterPixel + paddedRectHalf).ToLocation(map.Projection, newZoom).CastPoint()), TimeSpan.FromMilliseconds(10));
			};

			map.Zoom = 6;
			map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

			this.FindControl<Button>("homeButton").Click += (s, e) =>
				NavigateToHome();
			this.FindControl<Button>("settingsButton").Click += (s, e) =>
				SubWindowsService.Default.ShowSettingWindow();
			this.FindControl<Button>("updateButton").Click += (s, e) =>
				SubWindowsService.Default.ShowUpdateWindow();

			ConfigurationService.Current.Map.WhenAnyValue(x => x.ShowGrid).Subscribe(x => map.IsShowGrid = x);
			map.IsShowGrid = ConfigurationService.Current.Map.ShowGrid;

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
					map.Navigate(rect, ConfigurationService.Current.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero);
				else
					NavigateToHome();
			});
			MessageBus.Current.Listen<Core.Models.Events.RegistMapPositionRequested>().Subscribe(x =>
			{
				// �n�����W�ɍ��킹�邽�ߏ����������Ă���
				var halfPaddedRect = new PointD(map.PaddedRect.Width / 2, -map.PaddedRect.Height / 2);
				var centerPixel = map.CenterLocation.ToPixel(map.Projection, map.Zoom);

				ConfigurationService.Current.Map.Location1 = (centerPixel + halfPaddedRect).ToLocation(map.Projection, map.Zoom);
				ConfigurationService.Current.Map.Location2 = (centerPixel - halfPaddedRect).ToLocation(map.Projection, map.Zoom);
			});
			MessageBus.Current.Listen<Core.Models.Events.ShowSettingWindowRequested>().Subscribe(x => SubWindowsService.Default.ShowSettingWindow());
			MessageBus.Current.Listen<Core.Models.Events.ShowMainWindowRequested>().Subscribe(x =>
			{
				Topmost = true;
				Show();
				Topmost = false;
			});

			//this.GetObservable(IsExtendedIntoWindowDecorationsProperty)
			//	.Subscribe(x => this.FindControl<Grid>("titleBar").IsVisible = x);

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
				var grid = this.FindControl<Grid>("mainGrid");
				var desiredSize = new Size(DesiredSize.Width, DesiredSize.Height/* - (IsExtendedIntoWindowDecorations ? 30 : 0)*/ - Padding.Top - Padding.Bottom);
				var origSize = desiredSize * vm.Scale;
				var size = (origSize - desiredSize) / vm.Scale;
				grid.Margin = new Thickness(size.Width / 2, size.Height / 2, size.Width / 2, size.Height / 2);
			}
			base.OnMeasureInvalidated();
		}

		private MapControl? map;
		private Point _prevPos;
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
			ConfigurationService.Current.WindowState = WindowState;
			if (WindowState != WindowState.Minimized)
			{
				ConfigurationService.Current.WindowLocation = new(Position.X, Position.Y);
				if (WindowState != WindowState.Maximized)
					ConfigurationService.Current.WindowSize = new(ClientSize.Width, ClientSize.Height);
			}
			ConfigurationService.Save();
			return base.HandleClosing();
		}
	}
}
