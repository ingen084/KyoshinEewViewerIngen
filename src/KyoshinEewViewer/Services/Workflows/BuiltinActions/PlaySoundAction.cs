using Avalonia.Controls;
using ReactiveUI;
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

	private string GetFilePath(WorkflowEvent content)
	{
		if (string.IsNullOrWhiteSpace(FilePath))
			return "";

		var useParams = content.Variables;
		if (useParams == null || useParams.Count == 0)
			return FilePath;

		// Dictionary の Key を {(key1|key2)} みたいなパターンに置換する
		var pattern = $"{{({string.Join('|', useParams.Select(kvp => Regex.Escape(kvp.Key)))})}}";
		// このパターンを使って置き換え
		return Regex.Replace(FilePath, pattern, m => useParams[m.Groups[1].Value]);
	}

	public override Task ExecuteAsync(WorkflowEvent content)
	{
		GetFilePath(content);
		//TODO: Play sound	
		return Task.CompletedTask;
	}

	public void OpenSoundFile() { }
	public void Play() { }
}
