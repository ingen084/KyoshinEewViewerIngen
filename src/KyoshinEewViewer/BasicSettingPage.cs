using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Series;

namespace KyoshinEewViewer;
public class BasicSettingPage<T>(string? icon, string title, ISettingPage[] subPages) : ObservableObject, ISettingPage where T : Control, new()
{
	private bool _isVisible = true;
	public bool IsVisible
	{
		get => _isVisible;
		set => SetProperty(ref _isVisible, value);
	}

	public string? Icon => icon;
	public string Title => title;
	public Control DisplayControl => new T();

	public ISettingPage[] SubPages => subPages;
}

public class BasicSettingPage(string? icon, string title, ISettingPage[] subPages) : ObservableObject, ISettingPage
{
	private bool _isVisible = true;
	public bool IsVisible
	{
		get => _isVisible;
		set => SetProperty(ref _isVisible, value);
	}

	public string? Icon => icon;
	public string Title => title;
	public Control DisplayControl => new Panel();

	public ISettingPage[] SubPages => subPages;
}
