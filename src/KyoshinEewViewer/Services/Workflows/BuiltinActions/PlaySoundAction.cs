using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Core;
using Scriban;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public partial class PlaySoundAction : WorkflowAction
{
	[JsonIgnore]
	public override Control DisplayControl => new PlaySoundActionControl() { DataContext = this };

	[ObservableProperty]
	public partial string FilePath { get; set; } = "";

	[ObservableProperty]
	public partial double Volume { get; set; } = 1;

	[ObservableProperty]
	public partial bool WaitToEnd { get; set; } = false;

	public override async Task ExecuteAsync(WorkflowEvent content)
	{
		var template = Template.Parse(FilePath);
		var file = (await template.RenderAsync(content, m => m.Name)).Trim().Replace("\n", "");
		await ServiceLocator.Current.RequireService<SoundPlayerService>()
			.PlayAsync(file, Volume, WaitToEnd);
	}

	public void Play()
		=> ServiceLocator.Current.RequireService<SoundPlayerService>()
			.PlayAsync(FilePath, Volume, false).ConfigureAwait(false);
}
