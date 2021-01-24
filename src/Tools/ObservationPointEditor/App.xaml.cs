using System.Text;
using System.Windows;

namespace ObservationPointEditor
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			base.OnStartup(e);
		}
	}
}
