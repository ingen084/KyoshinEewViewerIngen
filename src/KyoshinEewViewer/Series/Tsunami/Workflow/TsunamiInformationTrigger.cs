using Avalonia.Controls;
using KyoshinEewViewer.Series.Tsunami.Models;
using KyoshinEewViewer.Services.Workflows;
using ReactiveUI;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.Tsunami.Workflow;

public class TsunamiInformationTrigger : WorkflowTrigger
{
	public static Dictionary<TsunamiLevel, string> LevelNames { get; } = new()
	{
		{ TsunamiLevel.None, "津波なし(すべて)" },
		{ TsunamiLevel.Forecast, "津波予報" },
		{ TsunamiLevel.Advisory, "津波注意報" },
		{ TsunamiLevel.Warning, "津波警報" },
		{ TsunamiLevel.MajorWarning, "大津波警報" },
	};

	public override Control DisplayControl => new TsunamiInformationTriggerControl { DataContext = this };

	private TsunamiLevel _level = TsunamiLevel.None;
	public TsunamiLevel Level
	{
		get => _level;
		set => this.RaiseAndSetIfChanged(ref _level, value);
	}

	private bool isExact = false;
	public bool IsExact
	{
		get => isExact;
		set => this.RaiseAndSetIfChanged(ref isExact, value);
	}

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

		// レベル
		if ((IsExact && e.Level != Level) || 
			(!IsExact && e.Level < Level))
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
