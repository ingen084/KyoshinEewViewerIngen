using KyoshinEewViewer.MapControl;
using KyoshinEewViewer.ViewModels;
using KyoshinMonitorLib;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Input;

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

			ViewModel.EventAggregator.GetEvent<Events.RegistMapPositionRequested>().Subscribe(() =>
			{
				// 地理座標に合わせるため少しいじっておく
				var halfPaddedRect = new Vector(map.PaddedRect.Width / 2, -map.PaddedRect.Height / 2);
				var centerPixel = map.CenterLocation.ToPixel(map.Zoom);

				ViewModel.ConfigService.Configuration.Map.Location1 = (centerPixel + halfPaddedRect).ToLocation(map.Zoom);
				ViewModel.ConfigService.Configuration.Map.Location2 = (centerPixel - halfPaddedRect).ToLocation(map.Zoom);
			});

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
				WindowStyle = WindowStyle.None;
				WindowState = WindowState.Maximized;
				IsFullScreen = true;
			};

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

			map.CenterLocation = new Location(36.474f, 135.264f);
			mapHomeButton.Click += (s, e) => NavigateToHome(true);
			Loaded += (s, e) => NavigateToHome(false);

			ResizeTimer = new Timer(s => Dispatcher.Invoke(() => NavigateToHome(true)), null, Timeout.Infinite, Timeout.Infinite);
		}

		private void NavigateToHome(bool animate)
			=> map.Navigate(new Rect(ViewModel.ConfigService.Configuration.Map.Location1.AsPoint(), ViewModel.ConfigService.Configuration.Map.Location2.AsPoint()), new Duration(animate ? TimeSpan.FromSeconds(.25) : TimeSpan.Zero));


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
		private void Map_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_prevPos = Mouse.GetPosition(map);
		}
		private void Map_MouseMove(object sender, MouseEventArgs e)
		{
			if (Mouse.LeftButton != MouseButtonState.Pressed ||
				map.IsNavigating ||
				ViewModel.ConfigService.Configuration.Map.DisableManualMapControl)
				return;
			var curPos = Mouse.GetPosition(map);
			var diff = _prevPos - curPos;
			_prevPos = curPos;
			map.CenterLocation = (map.CenterLocation.ToPixel(map.Zoom) + diff).ToLocation(map.Zoom);
		}
	}
}