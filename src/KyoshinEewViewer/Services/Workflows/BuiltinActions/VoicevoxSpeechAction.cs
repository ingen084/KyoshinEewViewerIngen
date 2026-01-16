using Avalonia.Controls;
using ReactiveUI;
using Splat;
using System;
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

	private bool _waitToEnd = true;
	public bool WaitToEnd
	{
		get => _waitToEnd;
		set => this.RaiseAndSetIfChanged(ref _waitToEnd, value);
	}

	private double _volume = 1;
	public double Volume
	{
		get => _volume;
		set => this.RaiseAndSetIfChanged(ref _volume, value);
	}

	public async override Task PrepareAsync(WorkflowEvent content)
	{
		var service = Locator.Current.GetService<VoicevoxService>();
		if (service == null)
			return;

		var renderedText = await Scriban.Template.Parse(TemplateText).RenderAsync(content, m => m.Name);
		if (string.IsNullOrWhiteSpace(renderedText))
			return;

		await service.PrepareAudioAsync(renderedText);
	}

	public async override Task ExecuteAsync(WorkflowEvent content)
	{
		var service = Locator.Current.GetService<VoicevoxService>();
		if (service == null)
			return;

		var renderedText = await Scriban.Template.Parse(TemplateText).RenderAsync(content, m => m.Name);
		if (string.IsNullOrWhiteSpace(renderedText))
			return;

		await service.PlayAsync(renderedText, Volume, WaitToEnd);
	}
}
