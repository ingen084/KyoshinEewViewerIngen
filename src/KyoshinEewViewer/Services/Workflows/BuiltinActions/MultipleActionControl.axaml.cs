using Avalonia.Controls;
using KyoshinEewViewer.CustomControl;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;
public partial class MultipleActionControl : UserControl
{
	public MultipleActionControl()
	{
		InitializeComponent();
	}

	private void OnItemMoved(object? sender, ItemMovedEventArgs e)
	{
		if (DataContext is MultipleAction multipleAction)
		{
			multipleAction.MoveAction(e.OldIndex, e.NewIndex);
		}
	}
}
