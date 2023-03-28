using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.CustomControl;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;

namespace KyoshinEewViewer.ViewModels;
public class SetupWizardWindowViewModel : ViewModelBase
{
	public KyoshinEewViewerConfiguration Config { get; }

	public Dictionary<string, string> RealtimeDataRenderModes { get; } = new()
	{
		{ nameof(RealtimeDataRenderMode.ShindoIcon), "震度アイコン" },
		{ nameof(RealtimeDataRenderMode.WideShindoIcon), "震度アイコン(ワイド)" },
		{ nameof(RealtimeDataRenderMode.RawColor), "数値変換前の色" },
		{ nameof(RealtimeDataRenderMode.ShindoIconAndRawColor), "震度アイコン+数値変換前の色" },
		{ nameof(RealtimeDataRenderMode.ShindoIconAndMonoColor), "震度アイコン+数値変換前の色(モノクロ)" },
	};

	private KeyValuePair<string, string> _selectedRealtimeDataRenderMode;

	public KeyValuePair<string, string> SelectedRealtimeDataRenderMode
	{
		get => _selectedRealtimeDataRenderMode;
		set => this.RaiseAndSetIfChanged(ref _selectedRealtimeDataRenderMode, value);
	}

	private RealtimeDataRenderMode _listRenderMode = RealtimeDataRenderMode.ShindoIcon;
	public RealtimeDataRenderMode ListRenderMode
	{
		get => _listRenderMode;
		set => this.RaiseAndSetIfChanged(ref _listRenderMode, value);
	}

	public SetupWizardWindowViewModel(KyoshinEewViewerConfiguration config)
	{
		Config = config;

		if (RealtimeDataRenderModes.ContainsKey(config.KyoshinMonitor.ListRenderMode))
			SelectedRealtimeDataRenderMode = RealtimeDataRenderModes.First(x => x.Key == config.KyoshinMonitor.ListRenderMode);
		else
			SelectedRealtimeDataRenderMode = RealtimeDataRenderModes.First();

		this.WhenAnyValue(x => x.SelectedRealtimeDataRenderMode)
			.Select(x => x.Key).Subscribe(x => config.KyoshinMonitor.ListRenderMode = x);

		config.KyoshinMonitor.WhenAnyValue(x => x.ListRenderMode)
			.Subscribe(x => ListRenderMode = Enum.TryParse<RealtimeDataRenderMode>(config.KyoshinMonitor.ListRenderMode, out var mode) ? mode : ListRenderMode);
		ListRenderMode = Enum.TryParse<RealtimeDataRenderMode>(config.KyoshinMonitor.ListRenderMode, out var mode) ? mode : ListRenderMode;
	}
}
