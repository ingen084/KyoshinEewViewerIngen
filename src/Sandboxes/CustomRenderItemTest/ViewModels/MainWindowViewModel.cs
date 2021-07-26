using ReactiveUI.Fody.Helpers;

namespace CustomRenderItemTest.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		[Reactive]
		public string Title { get; set; } = "KyoshinEewViewer for ingen (CustomRenderItemTest)";
	}
}
