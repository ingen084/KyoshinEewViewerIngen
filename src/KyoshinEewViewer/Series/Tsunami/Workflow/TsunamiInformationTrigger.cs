using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using KyoshinEewViewer.Series.Tsunami.Models;
using KyoshinEewViewer.Services.Workflows;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KyoshinEewViewer.Series.Tsunami.Workflow;

public partial class TsunamiInformationTrigger : WorkflowTrigger
{
	public override Type EventType => typeof(TsunamiInformationEvent);

	public static Dictionary<TsunamiLevel, string> LevelNames { get; } = new()
	{
		{ TsunamiLevel.None, "津波なし(すべて)" },
		{ TsunamiLevel.Forecast, "津波予報" },
		{ TsunamiLevel.Advisory, "津波注意報" },
		{ TsunamiLevel.Warning, "津波警報" },
		{ TsunamiLevel.MajorWarning, "大津波警報" },
	};

	[JsonIgnore]
	public override Control DisplayControl => new TsunamiInformationTriggerControl { DataContext = this };

	[ObservableProperty]
	public partial TsunamiLevel Level { get; set; } = TsunamiLevel.None;

	private bool isExact = false;
	public bool IsExact
	{
		get => isExact;
		set => SetProperty(ref isExact, value);
	}

	[ObservableProperty]
	public partial bool EnableIssued { get; set; } = true;

	[ObservableProperty]
	public partial bool EnableUpgraded { get; set; } = true;

	[ObservableProperty]
	public partial bool EnableDowngraded { get; set; } = true;

	[ObservableProperty]
	public partial bool EnableUpdated { get; set; } = true;

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
