using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Series.KyoshinMonitor.Services.Eew;

public class RealtimeEewController : EewController
{
	protected override bool IsReplay { get; } = false;
	protected override ILogger Logger { get; }

	public RealtimeEewController(ILogManager logManager, KyoshinEewViewerConfiguration config, TimerService timer, NotificationService notificationService, SoundPlayerService soundPlayer, EventHookService eventHook, WorkflowService workflowService)
		: base(config, timer, notificationService, soundPlayer, eventHook, workflowService)
	{
		SplatRegistrations.RegisterLazySingleton<RealtimeEewController>();

		Logger = logManager.GetLogger<RealtimeEewController>();
	}
}
