using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;

namespace KyoshinEewViewer.ViewModels
{
	public class MainWindowViewModel : ViewModelBase
	{
		private string title = "KyoshinEewViewer for ingen";
		public string Title
		{
			get => title;
			set => this.RaiseAndSetIfChanged(ref title, value);
		}
	}
}
