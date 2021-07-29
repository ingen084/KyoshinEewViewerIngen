using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Map;
using KyoshinEewViewer.Services;
using KyoshinEewViewer.ViewModels;
using ReactiveUI;
using System;
using System.Reactive.Linq;

namespace KyoshinEewViewer.Views
{
	public class MainWindow : FluentWindow
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

			// �t���X�N���@�\
			KeyDown += (s, e) =>
			{
				if (e.Key != Key.F11)
					return;

				if (IsFullScreen)
				{
					SystemDecorations = SystemDecorations.Full;
					WindowState = WindowState.Normal;
					IsFullScreen = false;
					return;
				}
				// ���łɍő剻����Ă���ꍇ���܂��t���X�N�ɂȂ�Ȃ��̂ň�U�ʏ��Ԃɖ߂�
				WindowState = WindowState.Normal;
				Dispatcher.UIThread.InvokeAsync(() =>
				{
					SystemDecorations = SystemDecorations.None;
					WindowState = WindowState.Maximized;
					IsFullScreen = true;
				});
			};

			// �}�b�v�\���I�v�V�����ɂ��{�^���̕\���R���g���[��
			ConfigurationService.Default.Map.WhenAnyValue(x => x.DisableManualMapControl).Subscribe(x => this.FindControl<Button>("homeButton").IsVisible = !x);
			this.FindControl<Button>("homeButton").IsVisible = !ConfigurationService.Default.Map.DisableManualMapControl;

			// �}�b�v�܂��̃n���h��
			map = this.FindControl<MapControl>("map");
			App.Selector?.WhenAnyValue(x => x.SelectedWindowTheme).Where(x => x != null)
					.Subscribe(x => map.RefleshResourceCache());
			var mapHitbox = this.FindControl<Grid>("mapHitbox");
			mapHitbox.PointerMoved += (s, e2) =>
			{
				var pointer = e2.GetCurrentPoint(this);
				var curPos = pointer.Position / ConfigurationService.Default.WindowScale;
				if (!ConfigurationService.Default.Map.DisableManualMapControl && pointer.Properties.IsLeftButtonPressed && !map.IsNavigating)
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
					_prevPos = pointer.Position / ConfigurationService.Default.WindowScale;
			};
			mapHitbox.PointerWheelChanged += (s, e) =>
			{
				if (ConfigurationService.Default.Map.DisableManualMapControl || map.IsNavigating)
					return;

				var pointer = e.GetCurrentPoint(this);
				var paddedRect = map.PaddedRect;
				var centerPix = map.CenterLocation.ToPixel(map.Projection, map.Zoom);
				var mousePos = pointer.Position / ConfigurationService.Default.WindowScale;
				var mousePix = new PointD(centerPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, centerPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);
				var mouseLoc = mousePix.ToLocation(map.Projection, map.Zoom);

				var newZoom = Math.Clamp(map.Zoom + e.Delta.Y * 0.25, map.MinZoom, map.MaxZoom);

				var newCenterPix = map.CenterLocation.ToPixel(map.Projection, newZoom);
				var goalMousePix = mouseLoc.ToPixel(map.Projection, newZoom);

				var newMousePix = new PointD(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

				map.Zoom = newZoom;
				map.CenterLocation = (map.CenterLocation.ToPixel(map.Projection, newZoom) - (goalMousePix - newMousePix)).ToLocation(map.Projection, newZoom);
			};

			map.Zoom = 6;
			map.CenterLocation = new KyoshinMonitorLib.Location(36.474f, 135.264f);

			this.FindControl<Button>("homeButton").Click += (s, e) =>
				NavigateToHome();
			this.FindControl<Button>("settingsButton").Click += (s, e) =>
				SubWindowsService.Default.ShowSettingWindow();
			this.FindControl<Button>("updateButton").Click += (s, e) =>
				SubWindowsService.Default.ShowUpdateWindow();

			// LayoutTransform�̃o�O�΍�̂��߃X�P�[���ω����ɂ�Padding��}�������邽�߂Ƀ��C�A�E�g������
			this.WhenAnyValue(x => x.DataContext)
				.Subscribe(c => (c as MainWindowViewModel)?.WhenAnyValue(x => x.Scale).Subscribe(s => InvalidateMeasure()));
			// �T�C�Y�ύX���Ƀ��C�A�E�g������
			this.WhenAnyValue(x => x.Bounds).Subscribe(x => InvalidateMeasure());

			MessageBus.Current.Listen<Core.Models.Events.MapNavigationRequested>().Subscribe(x =>
			{
				if (!ConfigurationService.Default.Map.AutoFocus)
					return;
				if (x.Bound is Rect rect)
					map.Navigate(rect, ConfigurationService.Default.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero);
				else
					NavigateToHome();
			});
			MessageBus.Current.Listen<Core.Models.Events.RegistMapPositionRequested>().Subscribe(x =>
			{
				// �n�����W�ɍ��킹�邽�ߏ����������Ă���
				var halfPaddedRect = new PointD(map.PaddedRect.Width / 2, -map.PaddedRect.Height / 2);
				var centerPixel = map.CenterLocation.ToPixel(map.Projection, map.Zoom);

				ConfigurationService.Default.Map.Location1 = (centerPixel + halfPaddedRect).ToLocation(map.Projection, map.Zoom);
				ConfigurationService.Default.Map.Location2 = (centerPixel - halfPaddedRect).ToLocation(map.Projection, map.Zoom);
			});
			MessageBus.Current.Listen<Core.Models.Events.ShowSettingWindowRequested>().Subscribe(x => SubWindowsService.Default.ShowSettingWindow());
			MessageBus.Current.Listen<Core.Models.Events.ShowMainWindowRequested>().Subscribe(x => Show());

			NavigateToHome();
		}

		private void NavigateToHome()
			=> map?.Navigate(
				new RectD(ConfigurationService.Default.Map.Location1.CastPoint(), ConfigurationService.Default.Map.Location2.CastPoint()),
				ConfigurationService.Default.Map.AutoFocusAnimation ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero);

		protected override void OnMeasureInvalidated()
		{
			if (DataContext is MainWindowViewModel vm)
			{
				var grid = this.FindControl<Grid>("mainGrid");
				var desiredSize = new Size(DesiredSize.Width, DesiredSize.Height - 31);
				var origSize = desiredSize * vm.Scale;
				var size = (origSize - desiredSize) / vm.Scale;
				grid.Margin = new Thickness(size.Width / 2, size.Height / 2, size.Width / 2, size.Height / 2);
			}
			base.OnMeasureInvalidated();
		}

		private MapControl? map;
		private Point _prevPos;

		protected override void HandleWindowStateChanged(WindowState state)
		{
			if (state == WindowState.Minimized && ConfigurationService.Default.Notification.HideWhenMinimizeWindow && NotificationService.Default.TrayIconAvailable)
			{
				Hide();
				return;
			}
			base.HandleWindowStateChanged(state);
		}
		protected override bool HandleClosing()
		{
			if (ConfigurationService.Default.Notification.HideWhenClosingWindow && NotificationService.Default.TrayIconAvailable)
			{
				Hide();
				return true;
			}
			return base.HandleClosing();
		}
	}
}
