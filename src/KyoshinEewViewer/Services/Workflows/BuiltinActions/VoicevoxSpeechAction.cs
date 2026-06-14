using Avalonia.Controls;
using ReactiveUI;
using Splat;
using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public class VoicevoxSpeechAction : WorkflowAction
{
	[JsonIgnore]
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

	private bool _sequentialMode;
	/// <summary>
	/// 改行で区切って順次読み上げるモード
	/// </summary>
	public bool SequentialMode
	{
		get => _sequentialMode;
		set => this.RaiseAndSetIfChanged(ref _sequentialMode, value);
	}

	private bool _interruptPrevious = true;
	/// <summary>
	/// 同じアクションで再生中の音声があれば中断して新しい再生を開始する
	/// </summary>
	public bool InterruptPrevious
	{
		get => _interruptPrevious;
		set => this.RaiseAndSetIfChanged(ref _interruptPrevious, value);
	}

	private static string[] SplitIntoSegments(string text)
		=> text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

	public async override Task PrepareAsync(WorkflowEvent content)
	{
		var service = Locator.Current.GetService<VoicevoxService>();
		if (service == null)
			return;

		var renderedText = await Scriban.Template.Parse(TemplateText).RenderAsync(content, m => m.Name);
		if (string.IsNullOrWhiteSpace(renderedText))
			return;

		if (SequentialMode)
		{
			var segments = SplitIntoSegments(renderedText);
			// 全セグメントの合成を並列で開始（完了を待たない）
			foreach (var segment in segments)
			{
				_ = service.PrepareAudioAsync(segment);
			}
		}
		else
		{
			await service.PrepareAudioAsync(renderedText);
		}
	}

	public async override Task ExecuteAsync(WorkflowEvent content)
	{
		var service = Locator.Current.GetService<VoicevoxService>();
		if (service == null)
			return;

		var renderedText = await Scriban.Template.Parse(TemplateText).RenderAsync(content, m => m.Name);
		if (string.IsNullOrWhiteSpace(renderedText))
			return;

		var owner = InterruptPrevious ? this : null;
		if (SequentialMode)
		{
			var segments = SplitIntoSegments(renderedText);
			await service.PrepareAndPlaySequentiallyAsync(segments, Volume, WaitToEnd, owner);
		}
		else
		{
			await service.PlayAsync(renderedText, Volume, WaitToEnd, owner);
		}
	}
}
