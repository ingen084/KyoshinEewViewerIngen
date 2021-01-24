using KyoshinEewViewer.MapControl;
using KyoshinEewViewer.Models.Events;
using KyoshinEewViewer.ViewModels;
using KyoshinMonitorLib;
using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace KyoshinEewViewer.Views
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private bool IsFullScreen { get; set; }
		private MainWindowViewModel ViewModel { get; }
		private Timer ResizeTimer { get; }

		public MainWindow()
		{
			InitializeComponent();

			ViewModel = DataContext as MainWindowViewModel;
			if (ViewModel == null)
				throw new NullReferenceException("ViewModelが正常にセットできていません");

			// 現在の表示位置を設定として登録させる
			ViewModel.EventAggregator.GetEvent<RegistMapPositionRequested>().Subscribe(() =>
			{
				// 地理座標に合わせるため少しいじっておく
				var halfPaddedRect = new Vector(map.PaddedRect.Width / 2, -map.PaddedRect.Height / 2);
				var centerPixel = map.CenterLocation.ToPixel(map.Zoom);

				ViewModel.ConfigService.Configuration.Map.Location1 = (centerPixel + halfPaddedRect).ToLocation(map.Zoom);
				ViewModel.ConfigService.Configuration.Map.Location2 = (centerPixel - halfPaddedRect).ToLocation(map.Zoom);
			});

			// メインウィンドウを表示する
			ViewModel.EventAggregator.GetEvent<ShowMainWindowRequested>().Subscribe(() =>
			{
				Show();
				Activate();
				Topmost = true;
				Topmost = false;
			});

			// フルスク化機能
			KeyDown += (s, e) =>
			{
				if (e.Key != Key.F11)
					return;

				if (IsFullScreen)
				{
					WindowStyle = WindowStyle.SingleBorderWindow;
					WindowState = WindowState.Normal;
					IsFullScreen = false;
					return;
				}
				// すでに最大化されている場合うまくフルスクにならないので一旦通常状態に戻す
				WindowState = WindowState.Normal;
				Dispatcher.Invoke(() =>
				{
					WindowStyle = WindowStyle.None;
					WindowState = WindowState.Maximized;
					IsFullScreen = true;
				});
			};

			// マップ表示オプションによるボタンの表示コントロール
			ViewModel.ConfigService.Configuration.Map.PropertyChanged += (s, e) =>
			{
				if (e.PropertyName != nameof(ViewModel.ConfigService.Configuration.Map.DisableManualMapControl))
					return;
				Dispatcher.Invoke(() =>
				{
					if (ViewModel.ConfigService.Configuration.Map.DisableManualMapControl)
						mapHomeButton.Visibility = Visibility.Collapsed;
					else
						mapHomeButton.Visibility = Visibility.Visible;
				});
			};
			if (ViewModel.ConfigService.Configuration.Map.DisableManualMapControl)
				mapHomeButton.Visibility = Visibility.Collapsed;
			else
				mapHomeButton.Visibility = Visibility.Visible;

			// 初期座標の設定
			map.CenterLocation = new Location(36.474f, 135.264f);
			mapHomeButton.Click += (s, e) => NavigateToHome(true);
			Loaded += (s, e) => NavigateToHome(false);

			ResizeTimer = new Timer(s => Dispatcher.Invoke(() => NavigateToHome(true)), null, Timeout.Infinite, Timeout.Infinite);
			LayoutTransform = new ScaleTransform(1.5, 1.5);
		}

		private void NavigateToHome(bool animate)
			=> map.Navigate(new Rect(ViewModel.ConfigService.Configuration.Map.Location1.AsPoint(), ViewModel.ConfigService.Configuration.Map.Location2.AsPoint()), new Duration(animate ? TimeSpan.FromSeconds(.3) : TimeSpan.Zero));


		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			if (!ViewModel.ConfigService.Configuration.Map.KeepRegion)
				return;
			ResizeTimer.Change(250, Timeout.Infinite);
		}

		private void Map_MouseWheel(object sender, MouseWheelEventArgs e)
		{
			if (map.IsNavigating || ViewModel.ConfigService.Configuration.Map.DisableManualMapControl)
				return;

			var paddedRect = map.PaddedRect;
			var centerPix = map.CenterLocation.ToPixel(map.Zoom);
			var mousePos = e.GetPosition(map);
			var mousePix = new Point(centerPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, centerPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);
			var mouseLoc = mousePix.ToLocation(map.Zoom);

			map.Zoom += e.Delta / 120 * 0.25;

			var newCenterPix = map.CenterLocation.ToPixel(map.Zoom);
			var goalMousePix = mouseLoc.ToPixel(map.Zoom);

			var newMousePix = new Point(newCenterPix.X + ((paddedRect.Width / 2) - mousePos.X) + paddedRect.Left, newCenterPix.Y + ((paddedRect.Height / 2) - mousePos.Y) + paddedRect.Top);

			map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) - (goalMousePix - newMousePix)).ToLocation(map.Zoom);
		}

		Point _prevPos;
		private void Map_MouseMove(object sender, MouseEventArgs e)
		{
			var curPos = Mouse.GetPosition(map);
			var diff = _prevPos - curPos;
			_prevPos = curPos;
			if (Mouse.LeftButton != MouseButtonState.Pressed ||
				map.IsNavigating ||
				ViewModel.ConfigService.Configuration.Map.DisableManualMapControl)
				return;
			map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) + diff).ToLocation(map.Zoom);
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);
			if (ViewModel.ConfigService.Configuration.Notification.Enable &&
				ViewModel.ConfigService.Configuration.Notification.HideWhenClosingWindow)
			{
				e.Cancel = true;
				Hide();
			}
		}
		protected override void OnStateChanged(EventArgs e)
		{
			base.OnStateChanged(e);
			if (WindowState == WindowState.Minimized &&
				ViewModel.ConfigService.Configuration.Notification.Enable &&
				ViewModel.ConfigService.Configuration.Notification.HideWhenMinimizeWindow)
			{
				WindowState = WindowState.Normal;
				Hide();
			}
		}
	}
}