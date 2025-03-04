using Avalonia;
using Avalonia.Controls;
using Avalonia.Xaml.Interactivity;

namespace KyoshinEewViewer;

public class BoundsObserver : Behavior<Control>
{
	public static readonly AttachedProperty<Rect> ObservedBoundsProperty =
		AvaloniaProperty.RegisterAttached<BoundsObserver, Control, Rect>("ObservedBounds", defaultBindingMode: Avalonia.Data.BindingMode.OneWayToSource);
	public static void SetObservedBounds(AvaloniaObject element, Rect value)
		=> element.SetValue(ObservedBoundsProperty, value);
	public static Rect GetObservedBounds(AvaloniaObject element)
		=> element.GetValue(ObservedBoundsProperty);

	protected override void OnAttached()
	{
		if (AssociatedObject is null)
			return;
		AssociatedObject.SizeChanged += Handler;
	}

	protected override void OnDetaching()
	{
		if (AssociatedObject is null)
			return;
		AssociatedObject.SizeChanged -= Handler;
	}

	private static void Handler(object? sender, SizeChangedEventArgs e)
	{
		if (sender is Control control)
		{
			control.SetValue(ObservedBoundsProperty, control.Bounds);
		}
	}
}
