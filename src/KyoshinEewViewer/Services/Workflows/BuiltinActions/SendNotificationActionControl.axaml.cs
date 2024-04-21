using Avalonia.Controls;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;
public partial class SendNotificationActionControl : UserControl
{
	public SendNotificationActionControl()
	{
		InitializeComponent();

		Editor.TextArea.TextView.Options.ShowSpaces = true;
		Editor.TextArea.TextView.Options.ShowTabs = true;
		Editor.Initialized += (_, _) =>
		{
			if (DataContext is SendNotificationAction action)
				Editor.Text = action.TemplateText;
		};

		var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
		Editor.InstallTextMate(registryOptions)
			.SetGrammar(registryOptions.GetScopeByLanguageId(registryOptions.GetLanguageByExtension(".razor").Id));
		Editor.TextChanged += (_, _) =>
		{
			if (DataContext is SendNotificationAction action)
				action.TemplateText = Editor.Text;
		};
	}
}
