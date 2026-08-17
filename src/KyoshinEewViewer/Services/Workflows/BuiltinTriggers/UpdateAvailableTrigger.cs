using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Services.Workflows.BuiltinTriggers;

public partial class UpdateAvailableTrigger : WorkflowTrigger
{
	public override Type EventType => typeof(UpdateAvailableEvent);
	[JsonIgnore]
	public override Control DisplayControl => new UpdateAvailableTriggerControl { DataContext = this };

	[ObservableProperty]
	public partial bool IsContinuous { get; set; } = false;
	
	public override bool CheckTrigger(WorkflowEvent content)
		=> content is UpdateAvailableEvent updateAvailableEvent && updateAvailableEvent.IsContinuous == IsContinuous;

	public override WorkflowEvent CreateTestEvent()
	{
		var random = new Random();
		return new UpdateAvailableEvent(IsContinuous && random.Next(0, 2) == 0, Assembly.GetExecutingAssembly()?.GetName().Version?.ToString() ?? "0.0.0");
	}
}

public partial class UpdateAvailableEvent(bool isContinuous, string latestVersion) : WorkflowEvent("UpdateAvailable", null)
{
	[Description("継続した通知(過去に発生した通知)かどうか")]
	public bool IsContinuous { get; } = isContinuous;

	[Description("利用可能な最新バージョン番号")]
	public string LatestVersion { get; } = latestVersion;
}
