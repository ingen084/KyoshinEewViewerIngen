using System.Windows;

namespace KyoshinEewViewer.Views
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private bool isFullScreen;
		public MainWindow()
		{
			InitializeComponent();

			KeyDown += (s, e) =>
			{
				if (e.Key != System.Windows.Input.Key.F11)
					return;

				if (isFullScreen)
				{
					WindowStyle = WindowStyle.SingleBorderWindow;
					WindowState = WindowState.Normal;
					isFullScreen = false;
					return;
				}
				WindowStyle = WindowStyle.None;
				WindowState = WindowState.Maximized;
				isFullScreen = true;
			};
		}
	}
}