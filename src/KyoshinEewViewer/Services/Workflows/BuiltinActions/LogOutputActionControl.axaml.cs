using System;
using Avalonia.Controls;
using AvaloniaEdit.TextMate;
using ReactiveUI;
using TextMateSharp.Grammars;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;
public partial class LogOutputActionControl : UserControl
{
	public LogOutputActionControl()
	{
		InitializeComponent();

		Editor.TextArea.TextView.Options.ShowSpaces = true;
		Editor.TextArea.TextView.Options.ShowTabs = true;
		Editor.Initialized += (_, _) =>
		{
			if (DataContext is LogOutputAction action)
				Editor.Text = action.TemplateText;
		};

		var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
		Editor.InstallTextMate(registryOptions)
			.SetGrammar(registryOptions.GetScopeByLanguageId(registryOptions.GetLanguageByExtension(".razor").Id));
		Editor.TextChanged += (_, _) =>
		{
			if (DataContext is LogOutputAction action)
				action.TemplateText = Editor.Text;
		};
	}
}
