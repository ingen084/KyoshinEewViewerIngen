using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using System;
using System.Collections.Generic;
using System.Text;

namespace KyoshinEewViewer.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		[Reactive]
		public string Title { get; set; } = "KyoshinEewViewer for ingen";
	}
}
