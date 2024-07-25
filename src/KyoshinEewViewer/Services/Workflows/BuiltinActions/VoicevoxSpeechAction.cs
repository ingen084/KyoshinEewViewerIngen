using Avalonia.Controls;
using ReactiveUI;
using Splat;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public class VoicevoxSpeechAction : WorkflowAction
{
	public override Control DisplayControl => new VoicevoxSpeechActionControl() { DataContext = this };

	private string _templateText = "アクションによる読み上げ";
	public string TemplateText
	{
		get => _templateText;
		set => this.RaiseAndSetIfChanged(ref _templateText, value);
	}

	private bool _waitToEnd = false;
	public bool WaitToEnd
	{
		get => _waitToEnd;
		set => this.RaiseAndSetIfChanged(ref _waitToEnd, value);
	}

	public async override Task ExecuteAsync(WorkflowEvent content)
	{
		var service = Locator.Current.GetService<VoicevoxService>();
		if (service == null)
			return;
		await service.PlayAsync(
			await Scriban.Template.Parse(TemplateText).RenderAsync(content, m => m.Name),
			WaitToEnd
		);
	}
}
