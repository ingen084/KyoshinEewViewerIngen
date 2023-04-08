using Avalonia.Controls;
using Avalonia.Threading;
using KyoshinEewViewer.Core;
using System;

namespace KyoshinEewViewer.Views;
public partial class DuplicateInstanceWarningWindow : Window
{
	public bool IsContinue { get; private set; }
	private IDisposable Timer { get; set; }

	public DuplicateInstanceWarningWindow()
	{
		InitializeComponent();

		continueButton.Tapped += (s, e) => { IsContinue = true; Close(); };
		exitButton.Tapped += (s, e) => Close();

		Timer = DispatcherTimer.RunOnce(CheckInstance, TimeSpan.FromSeconds(1));
	}

	private void CheckInstance()
	{
		if (!Utils.IsAppRunning
#if DEBUG
			|| true
#endif
			)
		{
			IsContinue = true;
			Close();
			return;
		}
		Timer = DispatcherTimer.RunOnce(CheckInstance, TimeSpan.FromSeconds(1));
	}

	protected override void OnClosed(EventArgs e)
	{
		Timer.Dispose();
		base.OnClosed(e);
	}
}
