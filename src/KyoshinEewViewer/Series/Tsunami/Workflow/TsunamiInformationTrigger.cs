using Avalonia.Controls;
using KyoshinEewViewer.Series.Tsunami.Models;
using KyoshinEewViewer.Services.Workflows;
using ReactiveUI;
using System;

namespace KyoshinEewViewer.Series.Tsunami.Workflow;

public class TsunamiInformationTrigger : WorkflowTrigger
{
	public override Control DisplayControl => new TsunamiInformationTriggerControl { DataContext = this };

	private bool _enableIssued = true;
	public bool EnableIssued
	{
		get => _enableIssued;
		set => this.RaiseAndSetIfChanged(ref _enableIssued, value);
	}

	private bool _enableUpgraded = true;
	public bool EnableUpgraded
	{
		get => _enableUpgraded;
		set => this.RaiseAndSetIfChanged(ref _enableUpgraded, value);
	}

	private bool _enableDowngraded = true;
	public bool EnableDowngraded
	{
		get => _enableDowngraded;
		set => this.RaiseAndSetIfChanged(ref _enableDowngraded, value);
	}

	private bool _enableUpdated = true;
	public bool EnableUpdated
	{
		get => _enableUpdated;
		set => this.RaiseAndSetIfChanged(ref _enableUpdated, value);
	}

	public override bool CheckTrigger(WorkflowEvent content)
	{
		if (content is not TsunamiInformationEvent e)
			return false;

		// 発表（未発表状態からの受信）
		if ((e.PreviousLevel <= TsunamiLevel.None) && e.Level > TsunamiLevel.None)
			return EnableIssued;

		// 解除・切り替え（レベル下降）
		if (e.PreviousLevel > TsunamiLevel.None && e.Level < e.PreviousLevel)
			return EnableDowngraded;

		// 切り替え（レベル上昇）
		if (e.PreviousLevel > TsunamiLevel.None && e.Level > e.PreviousLevel)
			return EnableUpgraded;

		// その他の更新
		return EnableUpdated;
	}
	public override WorkflowEvent CreateTestEvent()
	{
		var random = new Random();
		var info = new TsunamiInfo
		{
			EventId = "TestEvent",
			ReportedAt = DateTime.Now,
			ExpireAt = random.Next() % 2 == 0 ? null : DateTime.Now.AddHours(1),
			ForecastAreas = [new(0, "Test", "", "") { ArrivalTime = DateTime.Now }],
		};
		return new TsunamiInformationEvent(null)
		{
			IsTest = true,
			TsunamiInfo = info,
			PreviousLevel = TsunamiLevel.None
		};
	}
}
