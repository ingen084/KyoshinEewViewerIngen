using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.Workflows;
using Splat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KyoshinEewViewer.Services;

public class WorkflowService
{
	private static readonly List<WorkflowTriggerInfo> _allTriggers = [];
	public static IReadOnlyList<WorkflowTriggerInfo> AllTriggers => _allTriggers;
	private static readonly List<WorkflowActionInfo> _allActions = [];
	public static IReadOnlyList<WorkflowActionInfo> AllActions => _allActions;

	public static void RegisterTrigger<T>(string displayName) where T : WorkflowTrigger, new()
		=> _allTriggers.Add(new WorkflowTriggerInfo(typeof(T), displayName, () => new T()));
	public static void RegisterAction<T>(string displayName) where T : WorkflowAction, new()
		=> _allActions.Add(new WorkflowActionInfo(typeof(T), displayName, () => new T()));

	private ILogger Logger { get; }
	private KyoshinEewViewerConfiguration Configuration { get; }


}
