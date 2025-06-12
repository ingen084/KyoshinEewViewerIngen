using AvaloniaEdit.Utils;
using KyoshinEewViewer.Core;
using KyoshinEewViewer.Services.Workflows;
using Splat;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ZLinq;

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
		SplatRegistrations.RegisterLazySingleton<WorkflowService>();

		Logger = logManager.GetLogger<WorkflowService>();
	}

	public ObservableCollection<Workflow> UserWorkflows { get; } = new();
	public ObservableCollection<Workflow> SystemWorkflows { get; } = new();
	public ObservableCollection<Workflow> Workflows => UserWorkflows;

	public void LoadWorkflows()
	{
		UserWorkflows.Clear();
		UserWorkflows.AddRange(ConfigurationLoader.LoadWorkflows());
	}

	public void SaveWorkflows()
		=> ConfigurationLoader.SaveWorkflows(UserWorkflows.ToArray());


	public void PublishEvent(WorkflowEvent e)
	{
		Logger.LogDebug($"イベント {e.EventType}/{e.EventId} がトリガーされました");
		
		// ユーザーワークフローの実行
		var userWorkflowTasks = UserWorkflows.Where(w => w.Enabled).Select(async w =>
		{
			try
			{
				if (w.Trigger?.CheckTrigger(e) ?? false)
				{
					Logger.LogDebug($"ユーザーワークフロー {w.Name} がトリガーされました");
					if (w.Action is { } action)
						await action.ExecuteAsync(e);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, $"ユーザーワークフロー {w.Name} の実行中に例外が発生しました");
			}
		});

		// システムワークフローの実行
		var systemWorkflowTasks = SystemWorkflows.Where(w => w.Enabled).Select(async w =>
		{
			try
			{
				if (w.Trigger?.CheckTrigger(e) ?? false)
				{
					Logger.LogDebug($"システムワークフロー {w.Name} がトリガーされました");
					if (w.Action is { } action)
						await action.ExecuteAsync(e);
				}
			}
			catch (Exception ex)
			{
				Logger.LogError(ex, $"システムワークフロー {w.Name} の実行中に例外が発生しました");
			}
		});

		// ユーザーワークフローとシステムワークフローを並行実行
		Task.WhenAll(userWorkflowTasks.Concat(systemWorkflowTasks).ToArray()).ConfigureAwait(false);
	}
}
