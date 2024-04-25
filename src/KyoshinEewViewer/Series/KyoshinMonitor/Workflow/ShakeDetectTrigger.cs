using Avalonia.Controls;
using KyoshinEewViewer.Core.Models;
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
		return new ShakeDetectedEvent(
			DateTime.Now,
			new KyoshinEvent(DateTime.Now.AddSeconds(-random.Next(60)),
				new RealtimeObservationPoint(
					new KyoshinMonitorLib.ObservationPoint()
					{
						Code = "TEST",
						Name = "テスト",
						ClassificationId = 1,
						IsSuspended = false,
						Location = new(0, 0),
						OldLocation = new(0, 0),
						Point = new(0, 0),
						PrefectureClassificationId = 1,
						Region = "テスト",
						Type = random.Next() % 2 == 0 ? KyoshinMonitorLib.ObservationPointType.KiK_net : KyoshinMonitorLib.ObservationPointType.K_NET,
					}
				)
			)
			{
				Level = IsExact ? Level : random.Next(KyoshinEventLevel.Stronger - Level) + Level,
			}
		)
		{
			IsTest = true,
		};
	}
}
