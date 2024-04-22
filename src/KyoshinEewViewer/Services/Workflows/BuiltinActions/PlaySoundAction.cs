using Avalonia.Controls;
using ReactiveUI;
using Scriban;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services.Workflows.BuiltinActions;

public class PlaySoundAction : WorkflowAction
{
	public override Control DisplayControl => new PlaySoundActionControl() { DataContext = this };

	private string _filePath = "";
	public string FilePath
	{
		get => _filePath;
		set => this.RaiseAndSetIfChanged(ref _filePath, value);
	}

	private double _volume = 1;
	public double Volume
	{
		get => _volume;
		set => this.RaiseAndSetIfChanged(ref _volume, value);
	}

	private bool _allowMultiPlay = false;
	public bool AllowMultiPlay
	{
		get => _allowMultiPlay;
		set => this.RaiseAndSetIfChanged(ref _allowMultiPlay, value);
	}

	private bool _waitToEnd = false;
	public bool WaitToEnd
	{
		get => _waitToEnd;
		set => this.RaiseAndSetIfChanged(ref _waitToEnd, value);
	}

	public override async Task ExecuteAsync(WorkflowEvent content)
	{
		var template = Template.Parse(FilePath);
		var message = (await template.RenderAsync(content, m => m.Name)).Trim().Replace("\n", "");
		//TODO: Play sound
	}

	public void OpenSoundFile() { }
	public void Play() { }
}
