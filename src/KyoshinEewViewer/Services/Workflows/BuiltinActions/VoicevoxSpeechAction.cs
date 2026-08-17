using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using KyoshinEewViewer.Core;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public partial class VoicevoxSpeechAction : WorkflowAction
{
	[JsonIgnore]
	public override Control DisplayControl => new VoicevoxSpeechActionControl() { DataContext = this };

	[ObservableProperty]
	public partial string TemplateText { get; set; } = "アクションによる読み上げ";

	[ObservableProperty]
	public partial bool WaitToEnd { get; set; } = true;

	[ObservableProperty]
	public partial double Volume { get; set; } = 1;

	/// <summary>
	/// 改行で区切って順次読み上げるモード
	/// </summary>
	[ObservableProperty]
	public partial bool SequentialMode { get; set; }

	/// <summary>
	/// 同じアクションで再生中の音声があれば中断して新しい再生を開始する
	/// </summary>
	[ObservableProperty]
	public partial bool InterruptPrevious { get; set; } = true;

	private static string[] SplitIntoSegments(string text)
		=> text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

	public async override Task PrepareAsync(WorkflowEvent content)
	{
		var service = ServiceLocator.Current.GetService<VoicevoxService>();
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
		var service = ServiceLocator.Current.GetService<VoicevoxService>();
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
