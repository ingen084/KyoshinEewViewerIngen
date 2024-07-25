using Avalonia.Controls;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;
public partial class VoicevoxSpeechActionControl : UserControl
{
	public VoicevoxSpeechActionControl()
	{
		InitializeComponent();

		Editor.TextArea.TextView.Options.ShowSpaces = true;
		Editor.TextArea.TextView.Options.ShowTabs = true;
		Editor.Initialized += (_, _) =>
		{
			if (DataContext is VoicevoxSpeechAction action)
				Editor.Text = action.TemplateText;
		};

		var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
		Editor.InstallTextMate(registryOptions)
			.SetGrammar(registryOptions.GetScopeByLanguageId(registryOptions.GetLanguageByExtension(".razor").Id));
		Editor.TextChanged += (_, _) =>
		{
			if (DataContext is VoicevoxSpeechAction action)
				action.TemplateText = Editor.Text;
		};
	}
}
