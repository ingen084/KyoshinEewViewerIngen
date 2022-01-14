using Avalonia.Controls;
using Avalonia.Controls.Templates;
using KyoshinEewViewer.ViewModels;
using System;

namespace KyoshinEewViewer;

public class ViewLocator : IDataTemplate
{
	public static bool SupportsRecycling => false;

	public IControl Build(object data)
	{
		var name = data.GetType().FullName!.Replace("ViewModel", "View");
		var type = Type.GetType(name);

		if (type != null)
		{
			return (Control)Activator.CreateInstance(type)!;
		}
		else
		{
			return new TextBlock { Text = "Not Found: " + name };
		}
	}

	public bool Match(object data) => data is ViewModelBase;
}
