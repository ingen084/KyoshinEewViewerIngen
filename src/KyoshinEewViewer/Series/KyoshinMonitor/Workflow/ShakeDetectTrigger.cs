using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Core.Models.KyoshinMonitorObservationPoint;
using KyoshinEewViewer.Series.KyoshinMonitor.Models;
using KyoshinEewViewer.Services.Workflows;
using ReactiveUI;
using System;
using System.Collections.Generic;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Workflow;

public class ShakeDetectTrigger : WorkflowTrigger
{
	public static Dictionary<KyoshinEventLevel, string> LevelNames { get; } = new()
	{
		// { KyoshinEventLevel.Weaker, "微弱" },
		{ KyoshinEventLevel.Weak, "弱い(震度1未満)" },
		{ KyoshinEventLevel.Medium, "普通(震度1程度以上)" },
		{ KyoshinEventLevel.Strong, "強い(震度3程度以上)" },
		{ KyoshinEventLevel.Stronger, "非常に強い(震度5弱程度以上)" },
	};

	public override Control DisplayControl => new ShakeDetectTriggerControl() { DataContext = this };

	private KyoshinEventLevel _level = KyoshinEventLevel.Medium;
	public KyoshinEventLevel Level
	{
		get => _level;
		set => this.RaiseAndSetIfChanged(ref _level, value);
	}

	private bool _isExact = false;
	public bool IsExact
	{
		get => _isExact;
		set => this.RaiseAndSetIfChanged(ref _isExact, value);
	}

	public override bool CheckTrigger(WorkflowEvent content)
	{
		if (content is not ShakeDetectedEvent shakeEvent)
			return false;

		if (IsExact)
			return shakeEvent.Level == Level;

		return shakeEvent.Level >= Level;
	}

	public override WorkflowEvent CreateTestEvent()
	{
		var random = new Random();
		var level = IsExact ? Level : random.Next(KyoshinEventLevel.Stronger - Level) + Level;
		return new ShakeDetectedEvent(
			null,
			DateTime.Now,
			new KyoshinEvent(DateTime.Now.AddSeconds(-random.Next(60)),
				new RealtimeObservationPoint(
					new ObservationPointV2()
					{
						Code = "TEST",
						Name = "テスト",
						IsSuspended = false,
						Location = new(0, 0),
						Point = new(new(), new()),
						Region = "テスト",
						Type = random.Next() % 2 == 0 ? KyoshinMonitorLib.ObservationPointType.KiK_net : KyoshinMonitorLib.ObservationPointType.K_NET,
					}
				),
				ShakeDetectionParameters.Default.GetSeconds(level)
			)
			{
				Level = level,
			},
			random.Next() % 2 == 0
		)
		{
			IsTest = true,
		};
	}
}
