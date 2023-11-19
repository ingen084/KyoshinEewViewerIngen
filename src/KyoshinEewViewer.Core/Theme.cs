using Avalonia.Styling;
using ReactiveUI;
using System;

namespace KyoshinEewViewer.Core;

public class Theme(ThemeType type, string name, IStyle style, ThemeSelector selector) : ReactiveObject
{
	private string _name = name ?? throw new ArgumentNullException(nameof(name));
	private IStyle _style = style ?? throw new ArgumentNullException(nameof(style));
	private ThemeType _type = type;
	private ThemeSelector _selector = selector ?? throw new ArgumentNullException(nameof(selector));

	public string Name
	{
		get => _name;
		set => this.RaiseAndSetIfChanged(ref _name, value);
	}

	public IStyle Style
	{
		get => _style;
		set => this.RaiseAndSetIfChanged(ref _style, value);
	}

	public ThemeSelector Selector
	{
		get => _selector;
		set => this.RaiseAndSetIfChanged(ref _selector, value);
	}

	public ThemeType Type
	{
		get => _type;
		set => this.RaiseAndSetIfChanged(ref _type, value);
	}

	public void ApplyTheme()
	{
		switch (Type)
		{
			case ThemeType.Window:
				Selector.ApplyWindowTheme(this);
				break;
			case ThemeType.Intensity:
				Selector.ApplyIntensityTheme(this);
				break;
		};
	}
}

public enum ThemeType
{
	Window,
	Intensity,
}
