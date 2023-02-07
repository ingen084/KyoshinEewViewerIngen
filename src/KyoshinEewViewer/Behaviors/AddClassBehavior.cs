using Avalonia;
using Avalonia.Xaml.Interactivity;
using System;

namespace KyoshinEewViewer.Behaviors;

// https://gist.github.com/maxkatz6/2c765560767f20cf0483be8fac29ff22
public class AddClassBehavior : AvaloniaObject, IBehavior
{
	public AvaloniaObject? AssociatedObject { get; private set; }

	public string? Class
	{
		get => GetValue(ClassProperty);
		set => SetValue(ClassProperty, value);
	}

	public static readonly StyledProperty<string?> ClassProperty = AvaloniaProperty.Register<AddClassBehavior, string?>(nameof(Class), null);

	public bool IsEnabled
	{
		get => GetValue(IsEnabledProperty);
		set => SetValue(IsEnabledProperty, value);
	}

	public static readonly StyledProperty<bool> IsEnabledProperty = AvaloniaProperty.Register<AddClassBehavior, bool>(nameof(IsEnabled), false);

	public void Attach(AvaloniaObject? associatedObject)
	{
		if (associatedObject is not StyledElement styledElement)
		{
			throw new ArgumentException($"{nameof(AddClassBehavior)} supports only IStyledElement");
		}

		AssociatedObject = associatedObject;

		if (Class is string className)
		{
			if (IsEnabled)
			{
				if (!styledElement.Classes.Contains(className))
					styledElement.Classes.Add(className);
				return;
			}
			if (styledElement.Classes.Contains(className))
				styledElement.Classes.Remove(className);
		}
	}

	public void Detach()
	{
		IsEnabled = false;
		AssociatedObject = null;
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);

		if (AssociatedObject is not StyledElement styledElement)
			return;

		if (e.Property == ClassProperty)
		{
			if (e.OldValue is string oldClassName && styledElement.Classes.Contains(oldClassName))
				styledElement.Classes.Remove(oldClassName);
			if (e.NewValue is string newClassName && IsEnabled)
				styledElement.Classes.Add(newClassName);
		}
		else if (e.Property == IsEnabledProperty)
		{
			if (Class is string className)
			{
				if (IsEnabled)
				{
					if (!styledElement.Classes.Contains(className))
						styledElement.Classes.Add(className);
					return;
				}
				if (styledElement.Classes.Contains(className))
					styledElement.Classes.Remove(className);
			}
		}
	}
}
