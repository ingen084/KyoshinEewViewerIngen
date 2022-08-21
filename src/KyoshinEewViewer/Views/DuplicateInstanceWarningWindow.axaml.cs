using Avalonia.Controls;

namespace KyoshinEewViewer.Views;
public partial class DuplicateInstanceWarningWindow : Window
{
	public bool IsContinue { get; private set; }

	public DuplicateInstanceWarningWindow()
	{
		InitializeComponent();

		continueButton.Tapped += (s, e) => { IsContinue = true; Close(); };
		exitButton.Tapped += (s, e) => Close();
	}
}
