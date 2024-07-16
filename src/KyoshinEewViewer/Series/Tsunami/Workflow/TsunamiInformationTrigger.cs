using Avalonia.Controls;
using KyoshinEewViewer.Series.Tsunami.Models;
using KyoshinEewViewer.Services.Workflows;
using ReactiveUI;
using System;

namespace KyoshinEewViewer.Series.Tsunami.Workflow;

public class TsunamiInformationTrigger : WorkflowTrigger
{
	public override Control DisplayControl => new TsunamiInformationTriggerControl { DataContext = this };

	private bool _isSwitchOnly;
	public bool IsSwitchOnly
	{
		get => _isSwitchOnly;
		set => this.RaiseAndSetIfChanged(ref _isSwitchOnly, value);
	}

	public override bool CheckTrigger(WorkflowEvent content) => content is TsunamiInformationEvent e && (!IsSwitchOnly || e.Level == e.PreviousLevel);
	public override WorkflowEvent CreateTestEvent()
	{
		var random = new Random();
		var info = new TsunamiInfo
		{
			EventId = "TestEvent",
			ReportedAt = DateTime.Now,
			ExpireAt = random.Next() % 2 == 0 ? null : DateTime.Now.AddHours(1),
			ForecastAreas = [new(0, "Test", "", "", DateTime.Now)],
		};
		return new TsunamiInformationEvent
		{
			IsTest = true,
			TsunamiInfo = info,
			PreviousLevel = TsunamiLevel.None
		};
	}
}
