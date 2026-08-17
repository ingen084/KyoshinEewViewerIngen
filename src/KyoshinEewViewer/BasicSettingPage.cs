using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Series;

namespace KyoshinEewViewer;
public partial class BasicSettingPage<T>(string? icon, string title, ISettingPage[] subPages) : ObservableObject, ISettingPage where T : Control, new()
{
	[ObservableProperty]
	public partial bool IsVisible { get; set; } = true;

	public string? Icon => icon;
	public string Title => title;
	public Control DisplayControl => new T();

	public ISettingPage[] SubPages => subPages;
}

public partial class BasicSettingPage(string? icon, string title, ISettingPage[] subPages) : ObservableObject, ISettingPage
{
	[ObservableProperty]
	public partial bool IsVisible { get; set; } = true;

	public string? Icon => icon;
	public string Title => title;
	public Control DisplayControl => new Panel();

	public ISettingPage[] SubPages => subPages;
}
