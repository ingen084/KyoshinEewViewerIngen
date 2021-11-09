using Avalonia;
using Avalonia.Xaml.Interactivity;
using System;

namespace KyoshinEewViewer.Behaviors;

// https://gist.github.com/maxkatz6/2c765560767f20cf0483be8fac29ff22
public class AddClassBehavior : AvaloniaObject, IBehavior
{
	public IAvaloniaObject? AssociatedObject { get; private set; }

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

	public void Attach(IAvaloniaObject? associatedObject)
	{
		if (associatedObject is not IStyledElement styledElement)
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

	protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> e)
	{
		base.OnPropertyChanged(e);

		if (AssociatedObject is not IStyledElement styledElement)
			return;

		if (e.Property == ClassProperty)
		{
			if (e.OldValue.GetValueOrDefault<string?>() is string oldClassName)
			{
				if (styledElement.Classes.Contains(oldClassName))
					styledElement.Classes.Remove(oldClassName);
			}
			if (e.NewValue.GetValueOrDefault<string?>() is string newClassName)
			{
				if (IsEnabled)
					styledElement.Classes.Add(newClassName);
			}
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
