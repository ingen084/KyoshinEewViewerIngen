using KyoshinEewViewer.Core;
using KyoshinEewViewer.Core.Models;
using KyoshinEewViewer.Services.Workflows;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

	public WorkflowService(ILogManager logManager)
	{
		Logger = logManager.GetLogger<WorkflowService>();
	}

	public ObservableCollection<Workflow> Workflows { get; } = [];

	public async Task ExecuteWorkflow(WorkflowEvent e)
	{
		foreach (var workflow in Workflows)
		{
			if (!workflow.Enabled)
				continue;

			try
			{
				if (workflow.Trigger?.CheckTrigger(e) ?? false)
				{
					Logger.LogDebug($"ワークフロー {workflow.Name} がトリガーされました");
					if (workflow.Action is { } action)
						await action.ExecuteAsync(e);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, $"ワークフロー {workflow.Name} の実行中に例外が発生しました");
			}
		}
	}
}
